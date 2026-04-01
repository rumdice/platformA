using PlatformA.Library.Core;
using PlatformA.Ticketing.API.Services;
using PlatformA.Ticketing.API.Workers; // 추가
using RedLockNet.SERedis; // 추가
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Redis 공용 서비스 연결
builder.Services.AddSingleton<PlatformA.Library.Core.RedisManager>(PlatformA.Library.Core.RedisManager.Instance);

// redis 연결
RedisManager.Instance.Init();

// 2. 🔥 [추가] RedLock 팩토리 등록
//builder.Services.AddSingleton<RedLockFactory>(sp =>
//{
//    var redis = RedisManager.Instance.Connection;

//    // Redis 연결 객체를 리스트로 전달 (Multiplexer가 여러 개일 수도 있어서)
//    return RedLockFactory.Create(new List<RedLockEndPoint> { new RedLockEndPoint(redis.GetEndPoints()[0]) });
//});

// 수동 락 매니저 등록 (레디스 매니저 연동)
//builder.Services.AddSingleton<RedisLockManager>();

// 대기열 서비스 등록
builder.Services.AddSingleton<QueueService>();

// 대기열 서비스 워커 등록
builder.Services.AddSingleton<QueueWorkerService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<QueueWorkerService>()); // 백그라운드 워커 등록



builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddRateLimiter(options =>
{
    // 1) 로그인 전용: IP당 1분에 10번만 허용
    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.PermitLimit = 10;           // 최대 10번
        opt.Window = TimeSpan.FromMinutes(1); // 1분 윈도우
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;             // 대기 없이 즉시 거절
    });

    // 2) 대기열 API: IP당 1초에 5번만 허용
    options.AddFixedWindowLimiter("queue", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromSeconds(1);
        opt.QueueLimit = 0;
    });

    // 제한 초과 시 응답
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429; // Too Many Requests
        await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.");
    };
});


var app = builder.Build();

// 앱 종료 시 팩토리 정리 (Memory Leak 방지)
//app.Lifetime.ApplicationStopping.Register(() =>
//{
//    var factory = app.Services.GetService<RedLockFactory>();
//    factory?.Dispose();
//});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
