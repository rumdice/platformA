using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Matching.API.Hubs;
using PlatformA.Matching.API.OpenApi;
using PlatformA.Matching.API.Services;
using PlatformA.MySqlDB.Lib.DBWebApp;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ───────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddLog4Net("log4net.config");

// ── Services ──────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

// Redis — DI 팩토리
builder.Services.AddSingleton<PlatformA.Library.Core.RedisManager>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<PlatformA.Library.Core.RedisManager>>();
    PlatformA.Library.Core.RedisManager.Instance.Init(Consts.REDIS_CONNECTION_STRING, logger);
    return PlatformA.Library.Core.RedisManager.Instance;
});

// MySQL (db_WebApp) — 매칭 성사 시 MatchRecord 기록용
builder.Services.AddDbContextFactory<DbWebAppContext>(options =>
{
    options.UseMySql(
        Consts.MYSQL_WEBAPP_CONNECTION,
        new MySqlServerVersion(new Version(8, 0, 0)));
    options.UseSnakeCaseNamingConvention();
});

builder.Services.AddSignalR();

builder.Services.AddSingleton<EngineService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<EngineService>());

builder.Services.AddSingleton<GameMatchService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<GameMatchService>());

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLevel1", policy =>
    {
        policy.WithOrigins(
                "http://127.0.0.1:5500",
                "https://127.0.0.1:5500",
                "http://localhost:5500",
                "https://localhost:5500",
                "http://localhost:8080",
                "https://localhost:8080")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ── Health Checks ─────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddRedis(
        Consts.REDIS_CONNECTION_STRING,
        name: "redis",
        tags: ["readiness"]);

// ── App Pipeline ──────────────────────────────────────────────
var app = builder.Build();

// RedisManager는 DI 팩토리에서 Init + 로거 주입이 함께 처리됨
_ = app.Services.GetRequiredService<RedisManager>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseCors("AllowLevel1");
app.UseStaticFiles();
app.MapControllers();
app.MapHub<MatchingHub>("/hubs/matching");

app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/readyz", new HealthCheckOptions
{
    Predicate = h => h.Tags.Contains("readiness"),
    ResponseWriter = WriteJsonResponse
});

app.Run();

static Task WriteJsonResponse(HttpContext ctx, HealthReport report)
{
    ctx.Response.ContentType = "application/json; charset=utf-8";
    var result = JsonSerializer.Serialize(new
    {
        status = report.Status.ToString(),
        duration = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.ToDictionary(
            e => e.Key,
            e => new
            {
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            })
    }, new JsonSerializerOptions { WriteIndented = true });
    return ctx.Response.WriteAsync(result);
}

public partial class Program { }
