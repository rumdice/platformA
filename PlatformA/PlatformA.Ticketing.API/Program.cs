using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Library.RateLimit;
using PlatformA.Ticketing.API.Services;
using PlatformA.Ticketing.API.Workers;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ───────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddLog4Net("log4net.config");

// ── Services ──────────────────────────────────────────────────
// Redis 연결
RedisManager.Instance.Init(Consts.REDIS_CONNECTION_STRING);
builder.Services.AddSingleton<PlatformA.Library.Core.RedisManager>(PlatformA.Library.Core.RedisManager.Instance);

// 대기열 서비스 등록
builder.Services.AddSingleton<QueueService>();

// 대기열 워커 등록
builder.Services.AddSingleton<QueueWorkerService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<QueueWorkerService>());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Access Token을 입력하세요. 예: eyJhbGci..."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// Redis 기반 분산 Rate Limiter
builder.Services.AddSingleton<RedisRateLimiterService>(sp =>
{
    var svc = new RedisRateLimiterService(RedisManager.Instance);
    svc.AddPolicy("queue", permitLimit: 5, window: TimeSpan.FromSeconds(1));
    return svc;
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

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

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
