using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Data.Odbc;

var builder = WebApplication.CreateBuilder(args);

// Bind to specific port (override with ASPNETCORE_URLS)
var odbcUrl = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:5002";
builder.WebHost.UseUrls(odbcUrl);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ui", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.UseCors("ui");

string dsn = Environment.GetEnvironmentVariable("ODBC_DSN") ?? "AXEL_DSN";

IDbConnection CreateConnection() => new OdbcConnection($"DSN={dsn};");

// Simple in-memory sessions: sid -> userId
var SESSIONS = new Dictionary<string, int>();

int RequireAuth(HttpContext ctx)
{
    var sid = ctx.Request.Cookies["sessionId"];
    if (sid != null && SESSIONS.TryGetValue(sid, out var uid)) return uid;
    throw new Exception("UNAUTH");
}

bool IsAdmin(IDbConnection conn, int uid)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT role FROM users WHERE id = ?";
    var p = cmd.CreateParameter(); p.Value = uid; cmd.Parameters.Add(p);
    using var r = cmd.ExecuteReader();
    if (r.Read()) return string.Equals(r.GetString(0), "admin", StringComparison.OrdinalIgnoreCase);
    return false;
}

app.MapPost("/api/auth/register", async (HttpContext ctx, Dictionary<string,string> body) =>
{
    var fullName = body.GetValueOrDefault("fullName", "");
    var username = body.GetValueOrDefault("username", "");
    var password = body.GetValueOrDefault("password", "");
    var role = body.GetValueOrDefault("role", "user");
    if (role != "admin" && role != "user") role = "user";
    if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return Results.BadRequest(new { message = "Campos requeridos" });
    using var conn = CreateConnection(); await (conn as dynamic).OpenAsync();
    using (var chk = conn.CreateCommand())
    {
        chk.CommandText = "SELECT id FROM users WHERE username=?";
        var pu = chk.CreateParameter(); pu.Value = username; chk.Parameters.Add(pu);
        using var rr = await (chk as dynamic).ExecuteReaderAsync();
        if (await rr.ReadAsync()) return Results.Conflict(new { message = "Usuario ya existe" });
    }
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "INSERT INTO users(full_name, username, password_hash, role, is_active) VALUES (?, ?, SHA2(CONCAT('salt:', ?),256), ?, 1)";
        var pn = cmd.CreateParameter(); pn.Value = fullName; cmd.Parameters.Add(pn);
        var pu = cmd.CreateParameter(); pu.Value = username; cmd.Parameters.Add(pu);
        var pp = cmd.CreateParameter(); pp.Value = password; cmd.Parameters.Add(pp);
        var pr = cmd.CreateParameter(); pr.Value = role; cmd.Parameters.Add(pr);
        await (cmd as dynamic).ExecuteNonQueryAsync();
    }
    return Results.Ok(new { ok = true });
});

app.MapPost("/api/auth/login", async (HttpContext ctx, Dictionary<string,string> body) =>
{
    var username = body.GetValueOrDefault("username", "");
    var password = body.GetValueOrDefault("password", "");
    using var conn = CreateConnection(); await (conn as dynamic).OpenAsync();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT id, COALESCE(role,'user') AS role FROM users WHERE username=? AND password_hash=SHA2(CONCAT('salt:', ?),256) AND IFNULL(is_active,1)=1";
    var pu = cmd.CreateParameter(); pu.Value = username; cmd.Parameters.Add(pu);
    var pp = cmd.CreateParameter(); pp.Value = password; cmd.Parameters.Add(pp);
    using var r = await (cmd as dynamic).ExecuteReaderAsync();
    if (await r.ReadAsync())
    {
        var uid = r.GetInt32(0); var role = r.GetString(1);
        var sid = Guid.NewGuid().ToString(); SESSIONS[sid] = uid;
        ctx.Response.Cookies.Append("sessionId", sid, new CookieOptions { Path = "/" });
        return Results.Ok(new { ok = true, role });
    }
    return Results.Unauthorized();
});

app.MapPost("/api/auth/logout", (HttpContext ctx) =>
{
    var sid = ctx.Request.Cookies["sessionId"]; if (sid != null) SESSIONS.Remove(sid);
    ctx.Response.Cookies.Delete("sessionId");
    return Results.Ok(new { ok = true });
});

app.MapGet("/api/me", async (HttpContext ctx) =>
{
    int uid; try { uid = RequireAuth(ctx); } catch { return Results.Unauthorized(); }
    using var conn = CreateConnection(); await (conn as dynamic).OpenAsync();
    using var cmd = conn.CreateCommand(); cmd.CommandText = "SELECT id, username, full_name, COALESCE(role,'user') AS role FROM users WHERE id=?";
    var p = cmd.CreateParameter(); p.Value = uid; cmd.Parameters.Add(p);
    using var r = await (cmd as dynamic).ExecuteReaderAsync();
    if (await r.ReadAsync())
        return Results.Ok(new { id = r.GetInt32(0), username = r.GetString(1), fullName = r.GetString(2), role = r.GetString(3) });
    return Results.NotFound();
});

// Admin endpoints
app.MapGet("/api/admin/users", async Task<IResult> (HttpContext ctx, bool? includeInactive) =>
{
    int uid; try { uid = RequireAuth(ctx); } catch { return Results.Unauthorized(); }
    using var conn = CreateConnection(); await (conn as dynamic).OpenAsync();
    if (!IsAdmin(conn, uid)) return Results.StatusCode(403);
    var sql = (includeInactive ?? false)
        ? "SELECT id, username, full_name, COALESCE(role,'user') AS role, IFNULL(is_active,1) AS is_active FROM users ORDER BY id"
        : "SELECT id, username, full_name, COALESCE(role,'user') AS role, IFNULL(is_active,1) AS is_active FROM users WHERE IFNULL(is_active,1)=1 ORDER BY id";
    using var cmd = conn.CreateCommand(); cmd.CommandText = sql;
    var list = new List<object>();
    using var r = await (cmd as dynamic).ExecuteReaderAsync();
    while (await r.ReadAsync())
        list.Add(new { id = r.GetInt32(0), username = r.GetString(1), fullName = r.GetString(2), role = r.GetString(3), isActive = r.GetInt32(4) == 1 });
    return Results.Ok(list);
});

app.MapPost("/api/admin/users", async (HttpContext ctx, Dictionary<string,string> body) =>
{
    int uid; try { uid = RequireAuth(ctx); } catch { return Results.Unauthorized(); }
    using var conn = CreateConnection(); await (conn as dynamic).OpenAsync();
    if (!IsAdmin(conn, uid)) return Results.StatusCode(403);
    var fullName = body.GetValueOrDefault("fullName", "");
    var username = body.GetValueOrDefault("username", "");
    var password = body.GetValueOrDefault("password", "");
    var role = body.GetValueOrDefault("role", "user"); if (role != "admin" && role != "user") role = "user";
    if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return Results.BadRequest(new { message = "Campos requeridos" });
    using (var chk = conn.CreateCommand())
    { chk.CommandText = "SELECT id FROM users WHERE username=?"; var pu = chk.CreateParameter(); pu.Value = username; chk.Parameters.Add(pu);
      using var rr = await (chk as dynamic).ExecuteReaderAsync(); if (await rr.ReadAsync()) return Results.Conflict(new { message = "Usuario ya existe" }); }
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "INSERT INTO users(full_name, username, password_hash, role, is_active) VALUES (?, ?, SHA2(CONCAT('salt:', ?),256), ?, 1)";
        var pn = cmd.CreateParameter(); pn.Value = fullName; cmd.Parameters.Add(pn);
        var pu = cmd.CreateParameter(); pu.Value = username; cmd.Parameters.Add(pu);
        var pp = cmd.CreateParameter(); pp.Value = password; cmd.Parameters.Add(pp);
        var pr = cmd.CreateParameter(); pr.Value = role; cmd.Parameters.Add(pr);
        await (cmd as dynamic).ExecuteNonQueryAsync();
    }
    return Results.Ok(new { ok = true });
});

app.MapPut("/api/admin/users/{id:int}", async (HttpContext ctx, int id, Dictionary<string,string> body) =>
{
    int uid; try { uid = RequireAuth(ctx); } catch { return Results.Unauthorized(); }
    using var conn = CreateConnection(); await (conn as dynamic).OpenAsync(); if (!IsAdmin(conn, uid)) return Results.StatusCode(403);
    var role = body.GetValueOrDefault("role", "user"); if (role != "admin" && role != "user") role = "user";
    var fullName = body.ContainsKey("fullName") ? body["fullName"] : null;
    var username = body.ContainsKey("username") ? body["username"] : null;
    var setParts = new List<string>(); using var cmd = conn.CreateCommand();
    if (fullName != null) { setParts.Add("full_name=?"); var pn = cmd.CreateParameter(); pn.Value = fullName; cmd.Parameters.Add(pn); }
    if (username != null) { setParts.Add("username=?"); var pu = cmd.CreateParameter(); pu.Value = username; cmd.Parameters.Add(pu); }
    setParts.Add("role=?"); var pr = cmd.CreateParameter(); pr.Value = role; cmd.Parameters.Add(pr);
    cmd.CommandText = $"UPDATE users SET {string.Join(", ", setParts)} WHERE id=?"; var pid = cmd.CreateParameter(); pid.Value = id; cmd.Parameters.Add(pid);
    var affected = await (cmd as dynamic).ExecuteNonQueryAsync();
    if (affected == 1) return Results.Ok(new { ok = true });
    return Results.NotFound();
});

app.MapDelete("/api/admin/users/{id:int}", async (HttpContext ctx, int id) =>
{
    int uid; try { uid = RequireAuth(ctx); } catch { return Results.Unauthorized(); }
    using var conn = CreateConnection(); await (conn as dynamic).OpenAsync(); if (!IsAdmin(conn, uid)) return Results.StatusCode(403);
    using var cmd = conn.CreateCommand(); cmd.CommandText = "UPDATE users SET is_active=0 WHERE id=?"; var p = cmd.CreateParameter(); p.Value = id; cmd.Parameters.Add(p);
    var affected = await (cmd as dynamic).ExecuteNonQueryAsync();
    if (affected == 1) return Results.Ok(new { ok = true });
    return Results.NotFound();
});

app.MapPost("/api/admin/users/{id:int}/restore", async (HttpContext ctx, int id) =>
{
    int uid; try { uid = RequireAuth(ctx); } catch { return Results.Unauthorized(); }
    using var conn = CreateConnection(); await (conn as dynamic).OpenAsync(); if (!IsAdmin(conn, uid)) return Results.StatusCode(403);
    using var cmd = conn.CreateCommand(); cmd.CommandText = "UPDATE users SET is_active=1 WHERE id=?"; var p = cmd.CreateParameter(); p.Value = id; cmd.Parameters.Add(p);
    var affected = await (cmd as dynamic).ExecuteNonQueryAsync();
    if (affected == 1) return Results.Ok(new { ok = true });
    return Results.NotFound();
});

app.MapGet("/api/chilaquiles", async (HttpContext ctx, string? salsaType, string? protein, string? spiciness, int? page, int? pageSize, bool? includeInactive) =>
{
    try { RequireAuth(ctx); } catch { return Results.Unauthorized(); }
    var pageVal = (page.HasValue && page.Value > 0) ? page.Value : 1;
    var pageSizeVal = (pageSize.HasValue && pageSize.Value > 0) ? pageSize.Value : 10;
    int offset = (pageVal - 1) * pageSizeVal;

    using var conn = CreateConnection();
    await (conn as dynamic).OpenAsync();

    var cmd = conn.CreateCommand();
    var where = new List<string>();
    if (!(includeInactive ?? false)) { where.Add("is_active = 1"); }
    var paramValues = new List<object>();

    if (!string.IsNullOrEmpty(salsaType)) { where.Add("salsaType = ?"); paramValues.Add(salsaType); }
    if (!string.IsNullOrEmpty(protein)) { where.Add("protein = ?"); paramValues.Add(protein); }
    if (!string.IsNullOrEmpty(spiciness))
    {
        if (int.TryParse(spiciness, out var sp))
        {
            where.Add("spiciness = ?"); paramValues.Add(sp);
        }
    }

    string whereSql = where.Count > 0 ? (" WHERE " + string.Join(" AND ", where)) : "";

    // LIMIT/OFFSET inline (validados) porque el controlador ODBC no los parametriza
    cmd.CommandText = $"SELECT id,name,salsaType,protein,spiciness,price,createdAt,is_active FROM chilaquiles{whereSql} ORDER BY id LIMIT {pageSizeVal} OFFSET {offset}";
    foreach (var val in paramValues)
    {
        var p = cmd.CreateParameter(); p.Value = val; cmd.Parameters.Add(p);
    }

    var list = new List<object>();
    using var reader = await (cmd as dynamic).ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        list.Add(new
        {
            id = reader.GetInt32(0),
            name = reader.GetString(1),
            salsaType = reader.GetString(2),
            protein = reader.GetString(3),
            spiciness = reader.GetInt32(4),
            price = reader.GetDecimal(5),
            createdAt = reader.GetDateTime(6),
            isActive = reader.GetInt32(7) == 1
        });
    }
    return Results.Ok(list);
});

app.MapGet("/api/chilaquiles/{id:int}", async (HttpContext ctx, int id) =>
{
    try { RequireAuth(ctx); } catch { return Results.Unauthorized(); }
    using var conn = CreateConnection();
    await (conn as dynamic).OpenAsync();
    var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT id,name,salsaType,protein,spiciness,price,createdAt,is_active FROM chilaquiles WHERE id = ?";
    var p = cmd.CreateParameter(); p.Value = id; cmd.Parameters.Add(p);
    using var reader = await (cmd as dynamic).ExecuteReaderAsync();
    if (await reader.ReadAsync())
    {
        var item = new
        {
            id = reader.GetInt32(0),
            name = reader.GetString(1),
            salsaType = reader.GetString(2),
            protein = reader.GetString(3),
            spiciness = reader.GetInt32(4),
            price = reader.GetDecimal(5),
            createdAt = reader.GetDateTime(6),
            isActive = reader.GetInt32(7) == 1
        };
        return Results.Ok(item);
    }
    return Results.NotFound();
});

app.Run();
