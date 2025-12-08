using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System.Data;
using MySqlConnector;
using System.Data.Odbc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();

app.MapGet("/api/health", () => new { status = "ok" });

string driver = Environment.GetEnvironmentVariable("DB_DRIVER") ?? "ado"; // ado|odbc
string host = Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "localhost";
string port = Environment.GetEnvironmentVariable("MYSQL_PORT") ?? "3306";
string database = Environment.GetEnvironmentVariable("MYSQL_DATABASE") ?? "axel";
string user = Environment.GetEnvironmentVariable("MYSQL_USER") ?? "root";
string password = Environment.GetEnvironmentVariable("MYSQL_PASSWORD") ?? "Hola.123";

IDbConnection CreateConnection()
{
    if (driver.Equals("odbc", StringComparison.OrdinalIgnoreCase))
    {
        // Use DSN when present; else build connection string
        string dsn = Environment.GetEnvironmentVariable("ODBC_DSN") ?? "AXEL_DSN";
        return new OdbcConnection($"DSN={dsn};");
    }
    var cs = $"Server={host};Port={port};Database={database};User ID={user};Password={password};Ssl Mode=None;";
    return new MySqlConnection(cs);
}

app.MapGet("/api/chilaquiles", async (string? salsaType, string? protein, string? spiciness, int? page, int? pageSize) =>
{
    var pageVal = (page.HasValue && page.Value > 0) ? page.Value : 1;
    var pageSizeVal = (pageSize.HasValue && pageSize.Value > 0) ? pageSize.Value : 10;
    int offset = (pageVal - 1) * pageSizeVal;

    using var conn = CreateConnection();
    await (conn as dynamic).OpenAsync();

    var cmd = conn.CreateCommand();
    var where = new List<string>();
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
    cmd.CommandText = $"SELECT id,name,salsaType,protein,spiciness,price,createdAt FROM chilaquiles{whereSql} ORDER BY id LIMIT @limit OFFSET @offset";
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
            createdAt = reader.GetDateTime(6)
        });
    }
    return Results.Ok(list);
});

app.MapGet("/api/chilaquiles/{id:int}", async (int id) =>
{
    using var conn = CreateConnection();
    await (conn as dynamic).OpenAsync();
    var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT id,name,salsaType,protein,spiciness,price,createdAt FROM chilaquiles WHERE id = @id";
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
            createdAt = reader.GetDateTime(6)
        };
        return Results.Ok(item);
    }
    return Results.NotFound();
});

app.Run();
