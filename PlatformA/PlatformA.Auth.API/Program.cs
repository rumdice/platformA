using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PlatformA.Auth.API.OpenApi;
using Scalar.AspNetCore;
using PlatformA.Auth.API.HealthChecks;
using PlatformA.Auth.API.Services;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Library.RateLimit;
using PlatformA.MySqlDB.Lib.DBWebApp;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ──────────────────────────────────────────────────
// 기본 콘솔 프로바이더를 제거하고 log4net으로 단일화합니다.
// 로그는 logs/{date}.log 파일에도 함께 기록됩니다.
builder.Logging.ClearProviders();
builder.Logging.AddLog4Net("log4net.config");

// ── Services ─────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

// Redis — DI 팩토리: 로거 주입 후 Init (Polly 이벤트가 시작부터 정상 로깅됨)
builder.Services.AddSingleton<RedisManager>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<RedisManager>>();
    RedisManager.Instance.Init(Consts.REDIS_CONNECTION_STRING, logger);
    return RedisManager.Instance;
});

// MySQL (db_WebApp) — 멀티스레드 안전을 위해 IDbContextFactory 사용
// UseSnakeCaseNamingConventions: PascalCase 프로퍼티 → snake_case 컬럼명 자동 변환
builder.Services.AddDbContextFactory<DbWebAppContext>(options =>
{
    options.UseMySql(
        Consts.MYSQL_WEBAPP_CONNECTION,
        ServerVersion.AutoDetect(Consts.MYSQL_WEBAPP_CONNECTION));
    options.UseSnakeCaseNamingConvention();
});

// Refresh Token 관리 서비스 등록
builder.Services.AddSingleton<RefreshTokenService>();

// DB 기반 플레이어 인증 서비스
builder.Services.AddScoped<PlayerService>();

// Redis 기반 분산 Rate Limiter
builder.Services.AddSingleton<RedisRateLimiterService>(sp =>
{
    var svc = new RedisRateLimiterService(sp.GetRequiredService<RedisManager>());
    svc.AddPolicy("login", permitLimit: 10, window: TimeSpan.FromMinutes(1));
    return svc;
});

// ── Health Checks ─────────────────────────────────────────────
// /healthz  : Liveness  — 프로세스가 살아있는지 (외부 의존성 체크 없음)
// /readyz   : Readiness — Redis + MariaDB 연결 가능한지 (트래픽 수용 가능 여부)
builder.Services.AddHealthChecks()
    .AddRedis(
        Consts.REDIS_CONNECTION_STRING,
        name: "redis",
        tags: ["readiness"])
    .AddCheck<DbWebAppHealthCheck>(
        "mysql-webapp",
        tags: ["readiness"]);

// ── App Pipeline ──────────────────────────────────────────────
var app = builder.Build();

// RedisManager는 DI 팩토리에서 Init + 로거 주입이 함께 처리됨 — 별도 SetLogger 불필요
_ = app.Services.GetRequiredService<RedisManager>(); // 앱 시작 시 즉시 초기화

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Liveness: 외부 의존성 없이 프로세스 생존만 확인
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = _ => false // 모든 체크 제외 → 항상 200 Healthy
});

// Readiness: Redis + DB 연결 상태 포함한 JSON 응답
app.MapHealthChecks("/readyz", new HealthCheckOptions
{
    Predicate = h => h.Tags.Contains("readiness"),
    ResponseWriter = WriteJsonResponse
});

app.Run();

// ── Health Check JSON 응답 포맷 ───────────────────────────────
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
