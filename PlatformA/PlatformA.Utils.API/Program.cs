using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Library.Helper;
using PlatformA.Utils.API;
using PlatformA.Utils.API.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.


// sqlite 연결
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Redis — DI 팩토리
builder.Services.AddSingleton<RedisManager>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<RedisManager>>();
    RedisManager.Instance.Init(Consts.REDIS_CONNECTION_STRING, logger);
    return RedisManager.Instance;
});
// IConnectionMultiplexer — 컨트롤러 및 StatSyncsService DI 해결
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    sp.GetRequiredService<RedisManager>().Connection);

// Snowflake 등록 — SNOWFLAKE_WORKER_ID / SNOWFLAKE_DATACENTER_ID 환경변수로 스케일아웃 지원
var snowflakeWorkerId = int.Parse(
    Environment.GetEnvironmentVariable("SNOWFLAKE_WORKER_ID") ?? "1");
var snowflakeDatacenterId = int.Parse(
    Environment.GetEnvironmentVariable("SNOWFLAKE_DATACENTER_ID") ?? "1");
builder.Services.AddSingleton(new SnowflakeGenerator(snowflakeWorkerId, snowflakeDatacenterId));

// 백그라운드 서비스(Hosted Service) 등록
builder.Services.AddHostedService<StatSyncsService>();

// /healthz : Liveness  — 프로세스가 살아있는지 (외부 의존성 체크 없음)
// /readyz  : Readiness — Redis 연결 가능한지 (트래픽 수용 가능 여부)
builder.Services.AddHealthChecks()
    .AddRedis(
        Consts.REDIS_CONNECTION_STRING,
        name: "redis",
        tags: ["readiness"]);

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen(); API 서버 문서 비활성화

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()  // 보안상 실제 배포 시에는 "https://rumdice.github.io" 로 특정하는 게 좋습니다.
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();
_ = app.Services.GetService<RedisManager>(); // 앱 시작 시 즉시 초기화 (테스트 환경에서는 null)

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    //app.UseSwagger();
    //app.UseSwaggerUI();
}

// app.UseHttpsRedirection(); // 우리서버는 https만 받습니다.(307) -> 임시 주석. 

app.UseCors(); // CORS 미들웨어 적용

app.UseAuthorization();

app.MapControllers();

// Liveness: 외부 의존성 없이 프로세스 생존만 확인
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = _ => false // 모든 체크 제외 → 항상 200 Healthy
});

// Readiness: Redis 연결 상태 포함한 JSON 응답
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
                description = e.Value.Description
            })
    });
    return ctx.Response.WriteAsync(result);
}

public partial class Program { }
