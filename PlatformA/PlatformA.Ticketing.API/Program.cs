using PlatformA.Library.Core;
using PlatformA.Library.RateLimit;
using PlatformA.Ticketing.API.Services;
using PlatformA.Ticketing.API.Workers;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Redis 공용 서비스 연결
builder.Services.AddSingleton<PlatformA.Library.Core.RedisManager>(PlatformA.Library.Core.RedisManager.Instance);

// redis 연결
RedisManager.Instance.Init();

// 대기열 서비스 등록
builder.Services.AddSingleton<QueueService>();

// 대기열 서비스 워커 등록
builder.Services.AddSingleton<QueueWorkerService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<QueueWorkerService>());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Redis 기반 분산 Rate Limiter 등록
// ASP.NET Core 내장 RateLimiter(인스턴스별 메모리)를 대체합니다.
builder.Services.AddSingleton<RedisRateLimiterService>(sp =>
{
    var svc = new RedisRateLimiterService(RedisManager.Instance);
    // 대기열 API: IP당 1초에 5번
    svc.AddPolicy("queue", permitLimit: 5, window: TimeSpan.FromSeconds(1));
    return svc;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
