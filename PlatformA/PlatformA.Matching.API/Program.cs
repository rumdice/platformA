using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Matching.API.Hubs;
using PlatformA.Matching.API.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ───────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddLog4Net("log4net.config");

// ── Services ──────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<PlatformA.Library.Core.RedisManager>(PlatformA.Library.Core.RedisManager.Instance);
RedisManager.Instance.Init(Consts.REDIS_CONNECTION_STRING);

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
                "http://localhost:5500",
                "http://localhost:8080")
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

// RedisManager에 DI 로거 주입
RedisManager.Instance.SetLogger(app.Services.GetRequiredService<ILogger<RedisManager>>());

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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
