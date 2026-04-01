using PlatformA.Auth.API.Filters;
using PlatformA.Library.Core;
using PlatformA.Library.RateLimit;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Redis 연결 (분산 Rate Limiting용)
RedisManager.Instance.Init();
builder.Services.AddSingleton(RedisManager.Instance);

// Redis 기반 분산 Rate Limiter 등록
// ASP.NET Core 내장 RateLimiter(인스턴스별 메모리)를 대체합니다.
builder.Services.AddSingleton<RedisRateLimiterService>(sp =>
{
    var svc = new RedisRateLimiterService(RedisManager.Instance);
    // 로그인: IP당 1분에 10번
    svc.AddPolicy("login", permitLimit: 10, window: TimeSpan.FromMinutes(1));
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
