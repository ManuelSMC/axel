using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System.Data;
using MySqlConnector;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Bind to specific port (override with ASPNETCORE_URLS)
var adoUrl = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:5001";
builder.WebHost.UseUrls(adoUrl);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ui", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .DisallowCredentials());
});

// JWT bearer authentication (centralized issuer)
var jwtIssuer = builder.Configuration["JWT_ISSUER"] ?? Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "chilaquiles-auth";
var jwtAudience = builder.Configuration["JWT_AUDIENCE"] ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "chilaquiles-clients";
var jwtKeyStr = builder.Configuration["JWT_KEY"] ?? Environment.GetEnvironmentVariable("JWT_KEY") ?? "dev-secret-change";
byte[] jwtKeyBytes;
try { jwtKeyBytes = Convert.FromBase64String(jwtKeyStr); } catch { jwtKeyBytes = Encoding.UTF8.GetBytes(jwtKeyStr); }

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes),
        ClockSkew = TimeSpan.Zero
    };
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = ctx =>
        {
            Console.WriteLine($"[JWT] Auth failed: {ctx.Exception?.GetType().Name} - {ctx.Exception?.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = ctx =>
        {
            Console.WriteLine("[JWT] Token validated");
            return Task.CompletedTask;
        }
    };
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.UseCors("ui");
app.UseAuthentication();
app.UseAuthorization();

// Hardcoded driver for this service (ADO.NET)

string host = Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "localhost";
string port = Environment.GetEnvironmentVariable("MYSQL_PORT") ?? "3306";
string database = Environment.GetEnvironmentVariable("MYSQL_DATABASE") ?? "axel";
string user = Environment.GetEnvironmentVariable("MYSQL_USER") ?? "root";
string password = Environment.GetEnvironmentVariable("MYSQL_PASSWORD") ?? "Hola.123";

IDbConnection CreateConnection()
{
    var cs = $"Server={host};Port={port};Database={database};User ID={user};Password={password};Ssl Mode=None;";
    return new MySqlConnection(cs);
}

// Require JWT auth and resolve current user id claim
int RequireAuth(HttpContext ctx)
{
    if (ctx.User?.Identity?.IsAuthenticated == true)
    {
        var uidClaim = ctx.User.FindFirst("uid")?.Value;
        if (int.TryParse(uidClaim, out var uid)) return uid;
    }
    throw new Exception("UNAUTH");
}

bool IsAdmin(IDbConnection conn, int uid)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT role FROM users WHERE id = @id";
    var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = uid; cmd.Parameters.Add(p);
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
        chk.CommandText = "SELECT id FROM users WHERE username=@u";
        var pu = chk.CreateParameter(); pu.ParameterName = "@u"; pu.Value = username; chk.Parameters.Add(pu);
        using var rr = await (chk as dynamic).ExecuteReaderAsync();
        if (await rr.ReadAsync()) return Results.Conflict(new { message = "Usuario ya existe" });
    }
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "INSERT INTO users(full_name, username, password_hash, role, is_active) VALUES (@n, @u, SHA2(CONCAT('salt:', @p),256), @r, 1)";
        var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; pn.Value = fullName; cmd.Parameters.Add(pn);
        var pu = cmd.CreateParameter(); pu.ParameterName = "@u"; pu.Value = username; cmd.Parameters.Add(pu);
        var pp = cmd.CreateParameter(); pp.ParameterName = "@p"; pp.Value = password; cmd.Parameters.Add(pp);
        var pr = cmd.CreateParameter(); pr.ParameterName = "@r"; pr.Value = role; cmd.Parameters.Add(pr);
        await (cmd as dynamic).ExecuteNonQueryAsync();
    }
    return Results.Ok(new { ok = true });
});

// Login endpoint disabled here; JWT should be issued by central auth service (Java).
app.MapPost("/api/auth/login", () => Results.StatusCode(501));

// Logout is client-side for JWT; server does not manage sessions
app.MapPost("/api/auth/logout", () => Results.Ok(new { ok = true }));

app.MapGet("/api/me", async (HttpContext ctx) =>
{
    int uid; try { uid = RequireAuth(ctx); } catch { return Results.Unauthorized(); }
    using var conn = CreateConnection(); await (conn as dynamic).OpenAsync();
    using var cmd = conn.CreateCommand(); cmd.CommandText = "SELECT id, username, full_name, COALESCE(role,'user') AS role FROM users WHERE id=@id";
    var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = uid; cmd.Parameters.Add(p);
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
    { chk.CommandText = "SELECT id FROM users WHERE username=@u"; var pu = chk.CreateParameter(); pu.ParameterName = "@u"; pu.Value = username; chk.Parameters.Add(pu);
      using var rr = await (chk as dynamic).ExecuteReaderAsync(); if (await rr.ReadAsync()) return Results.Conflict(new { message = "Usuario ya existe" }); }
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "INSERT INTO users(full_name, username, password_hash, role, is_active) VALUES (@n, @u, SHA2(CONCAT('salt:', @p),256), @r, 1)";
        var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; pn.Value = fullName; cmd.Parameters.Add(pn);
        var pu = cmd.CreateParameter(); pu.ParameterName = "@u"; pu.Value = username; cmd.Parameters.Add(pu);
        var pp = cmd.CreateParameter(); pp.ParameterName = "@p"; pp.Value = password; cmd.Parameters.Add(pp);
        var pr = cmd.CreateParameter(); pr.ParameterName = "@r"; pr.Value = role; cmd.Parameters.Add(pr);
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
    var setParts = new List<string>();
    using var cmd = conn.CreateCommand();
    if (fullName != null) { setParts.Add("full_name=@n"); var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; pn.Value = fullName; cmd.Parameters.Add(pn); }
    if (username != null) { setParts.Add("username=@u"); var pu = cmd.CreateParameter(); pu.ParameterName = "@u"; pu.Value = username; cmd.Parameters.Add(pu); }
    setParts.Add("role=@r"); var pr = cmd.CreateParameter(); pr.ParameterName = "@r"; pr.Value = role; cmd.Parameters.Add(pr);
    cmd.CommandText = $"UPDATE users SET {string.Join(", ", setParts)} WHERE id=@id"; var pid = cmd.CreateParameter(); pid.ParameterName = "@id"; pid.Value = id; cmd.Parameters.Add(pid);
    var affected = await (cmd as dynamic).ExecuteNonQueryAsync();
    if (affected == 1) return Results.Ok(new { ok = true });
    return Results.NotFound();
});

app.MapDelete("/api/admin/users/{id:int}", async (HttpContext ctx, int id) =>
{
    int uid; try { uid = RequireAuth(ctx); } catch { return Results.Unauthorized(); }
    using var conn = CreateConnection(); await (conn as dynamic).OpenAsync(); if (!IsAdmin(conn, uid)) return Results.StatusCode(403);
    using var cmd = conn.CreateCommand(); cmd.CommandText = "UPDATE users SET is_active=0 WHERE id=@id"; var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = id; cmd.Parameters.Add(p);
    var affected = await (cmd as dynamic).ExecuteNonQueryAsync();
    if (affected == 1) return Results.Ok(new { ok = true });
    return Results.NotFound();
});

app.MapPost("/api/admin/users/{id:int}/restore", async (HttpContext ctx, int id) =>
{
    int uid; try { uid = RequireAuth(ctx); } catch { return Results.Unauthorized(); }
    using var conn = CreateConnection(); await (conn as dynamic).OpenAsync(); if (!IsAdmin(conn, uid)) return Results.StatusCode(403);
    using var cmd = conn.CreateCommand(); cmd.CommandText = "UPDATE users SET is_active=1 WHERE id=@id"; var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = id; cmd.Parameters.Add(p);
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
    if (!(includeInactive ?? false)) where.Add("is_active = 1");

    if (!string.IsNullOrEmpty(salsaType)) { where.Add("salsaType = @salsaType"); var p = cmd.CreateParameter(); p.ParameterName = "@salsaType"; p.Value = salsaType; cmd.Parameters.Add(p); }
    if (!string.IsNullOrEmpty(protein)) { where.Add("protein = @protein"); var p = cmd.CreateParameter(); p.ParameterName = "@protein"; p.Value = protein; cmd.Parameters.Add(p); }
    if (!string.IsNullOrEmpty(spiciness))
    {
        if (int.TryParse(spiciness, out var sp))
        {
            where.Add("spiciness = @spiciness"); var p = cmd.CreateParameter(); p.ParameterName = "@spiciness"; p.Value = sp; cmd.Parameters.Add(p);
        }
    }

    string whereSql = where.Count > 0 ? (" WHERE " + string.Join(" AND ", where)) : "";

    cmd.CommandText = $"SELECT id,name,salsaType,protein,spiciness,price,createdAt,is_active FROM chilaquiles{whereSql} ORDER BY id LIMIT @limit OFFSET @offset";
    var pl = cmd.CreateParameter(); pl.ParameterName = "@limit"; pl.Value = pageSizeVal; cmd.Parameters.Add(pl);
    var po = cmd.CreateParameter(); po.ParameterName = "@offset"; po.Value = offset; cmd.Parameters.Add(po);

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
    cmd.CommandText = "SELECT id,name,salsaType,protein,spiciness,price,createdAt,is_active FROM chilaquiles WHERE id = @id";
    var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = id; cmd.Parameters.Add(p);
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

// Create chilaquiles
app.MapPost("/api/chilaquiles", async (HttpContext ctx, Dictionary<string, System.Text.Json.JsonElement> body) =>
{
    try { RequireAuth(ctx); } catch { return Results.Unauthorized(); }
    string name = body.ContainsKey("name") ? (body["name"].ValueKind == System.Text.Json.JsonValueKind.String ? body["name"].GetString() ?? "" : body["name"].ToString()) : "";
    string salsaType = body.ContainsKey("salsaType") ? (body["salsaType"].ValueKind == System.Text.Json.JsonValueKind.String ? body["salsaType"].GetString() ?? "" : body["salsaType"].ToString()) : "";
    string protein = body.ContainsKey("protein") ? (body["protein"].ValueKind == System.Text.Json.JsonValueKind.String ? body["protein"].GetString() ?? "" : body["protein"].ToString()) : "";
    int spiciness = body.ContainsKey("spiciness") ? (body["spiciness"].ValueKind == System.Text.Json.JsonValueKind.Number ? body["spiciness"].GetInt32() : int.TryParse(body["spiciness"].ToString(), out var sp) ? sp : 0) : 0;
    decimal price = body.ContainsKey("price") ? (body["price"].ValueKind == System.Text.Json.JsonValueKind.Number ? body["price"].GetDecimal() : decimal.TryParse(body["price"].ToString(), out var pr) ? pr : 0m) : 0m;
    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(salsaType) || string.IsNullOrWhiteSpace(protein))
        return Results.BadRequest(new { message = "Campos requeridos" });
    using var conn = CreateConnection(); await (conn as dynamic).OpenAsync();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "INSERT INTO chilaquiles(name,salsaType,protein,spiciness,price,createdAt,is_active) VALUES (@n,@s,@p,@sp,@pr,NOW(),1)";
    var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; pn.Value = name; pn.DbType = System.Data.DbType.String; cmd.Parameters.Add(pn);
    var ps = cmd.CreateParameter(); ps.ParameterName = "@s"; ps.Value = salsaType; ps.DbType = System.Data.DbType.String; cmd.Parameters.Add(ps);
    var pp = cmd.CreateParameter(); pp.ParameterName = "@p"; pp.Value = protein; pp.DbType = System.Data.DbType.String; cmd.Parameters.Add(pp);
    var psp = cmd.CreateParameter(); psp.ParameterName = "@sp"; psp.Value = spiciness; psp.DbType = System.Data.DbType.Int32; cmd.Parameters.Add(psp);
    var ppr = cmd.CreateParameter(); ppr.ParameterName = "@pr"; ppr.Value = price; ppr.DbType = System.Data.DbType.Decimal; cmd.Parameters.Add(ppr);
    var affected = await (cmd as dynamic).ExecuteNonQueryAsync();
    if (affected == 1)
    {
        using var idCmd = conn.CreateCommand(); idCmd.CommandText = "SELECT LAST_INSERT_ID()";
        using var r = await (idCmd as dynamic).ExecuteReaderAsync();
        if (await r.ReadAsync())
            return Results.Ok(new { ok = true, id = r.GetInt32(0) });
        return Results.Ok(new { ok = true });
    }
    return Results.StatusCode(500);
});

// Update chilaquiles
app.MapPut("/api/chilaquiles/{id:int}", async (HttpContext ctx, int id, Dictionary<string, System.Text.Json.JsonElement> body) =>
{
    try { RequireAuth(ctx); } catch { return Results.Unauthorized(); }
    string name = body.ContainsKey("name") ? (body["name"].ValueKind == System.Text.Json.JsonValueKind.String ? body["name"].GetString() ?? "" : body["name"].ToString()) : "";
    string salsaType = body.ContainsKey("salsaType") ? (body["salsaType"].ValueKind == System.Text.Json.JsonValueKind.String ? body["salsaType"].GetString() ?? "" : body["salsaType"].ToString()) : "";
    string protein = body.ContainsKey("protein") ? (body["protein"].ValueKind == System.Text.Json.JsonValueKind.String ? body["protein"].GetString() ?? "" : body["protein"].ToString()) : "";
    int spiciness = body.ContainsKey("spiciness") ? (body["spiciness"].ValueKind == System.Text.Json.JsonValueKind.Number ? body["spiciness"].GetInt32() : int.TryParse(body["spiciness"].ToString(), out var sp) ? sp : 0) : 0;
    decimal price = body.ContainsKey("price") ? (body["price"].ValueKind == System.Text.Json.JsonValueKind.Number ? body["price"].GetDecimal() : decimal.TryParse(body["price"].ToString(), out var pr) ? pr : 0m) : 0m;
    using var conn = CreateConnection(); await (conn as dynamic).OpenAsync();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "UPDATE chilaquiles SET name=@n, salsaType=@s, protein=@p, spiciness=@sp, price=@pr WHERE id=@id";
    var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; pn.Value = name; pn.DbType = System.Data.DbType.String; cmd.Parameters.Add(pn);
    var ps = cmd.CreateParameter(); ps.ParameterName = "@s"; ps.Value = salsaType; ps.DbType = System.Data.DbType.String; cmd.Parameters.Add(ps);
    var pp = cmd.CreateParameter(); pp.ParameterName = "@p"; pp.Value = protein; pp.DbType = System.Data.DbType.String; cmd.Parameters.Add(pp);
    var psp = cmd.CreateParameter(); psp.ParameterName = "@sp"; psp.Value = spiciness; psp.DbType = System.Data.DbType.Int32; cmd.Parameters.Add(psp);
    var ppr = cmd.CreateParameter(); ppr.ParameterName = "@pr"; ppr.Value = price; ppr.DbType = System.Data.DbType.Decimal; cmd.Parameters.Add(ppr);
    var pid = cmd.CreateParameter(); pid.ParameterName = "@id"; pid.Value = id; pid.DbType = System.Data.DbType.Int32; cmd.Parameters.Add(pid);
    var affected = await (cmd as dynamic).ExecuteNonQueryAsync();
    if (affected == 1) return Results.Ok(new { ok = true });
    return Results.NotFound();
});

// Soft delete
app.MapDelete("/api/chilaquiles/{id:int}", async (HttpContext ctx, int id) =>
{
    try { RequireAuth(ctx); } catch { return Results.Unauthorized(); }
    using var conn = CreateConnection(); await (conn as dynamic).OpenAsync();
    using var cmd = conn.CreateCommand(); cmd.CommandText = "UPDATE chilaquiles SET is_active=0 WHERE id=@id";
    var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = id; cmd.Parameters.Add(p);
    var affected = await (cmd as dynamic).ExecuteNonQueryAsync();
    if (affected == 1) return Results.Ok(new { ok = true });
    return Results.NotFound();
});

// Restore
app.MapPost("/api/chilaquiles/{id:int}/restore", async (HttpContext ctx, int id) =>
{
    try { RequireAuth(ctx); } catch { return Results.Unauthorized(); }
    using var conn = CreateConnection(); await (conn as dynamic).OpenAsync();
    using var cmd = conn.CreateCommand(); cmd.CommandText = "UPDATE chilaquiles SET is_active=1 WHERE id=@id";
    var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = id; cmd.Parameters.Add(p);
    var affected = await (cmd as dynamic).ExecuteNonQueryAsync();
    if (affected == 1) return Results.Ok(new { ok = true });
    return Results.NotFound();
});

app.Run();
