using Microsoft.EntityFrameworkCore;
using PlatformA.Auth.API.Services;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Library.RateLimit;
using PlatformA.MySqlDB.Lib.DBWebApp;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Redis 연결 (분산 Rate Limiting용)
RedisManager.Instance.Init(Consts.REDIS_CONNECTION_STRING);
builder.Services.AddSingleton(RedisManager.Instance);

// MySQL (db_WebApp) — 멀티스레드 안전을 위해 IDbContextFactory 사용
builder.Services.AddDbContextFactory<DbWebAppContext>(options =>
    options.UseMySql(
        Consts.MYSQL_WEBAPP_CONNECTION,
        ServerVersion.AutoDetect(Consts.MYSQL_WEBAPP_CONNECTION)
    ));

// Refresh Token 관리 서비스 등록
builder.Services.AddSingleton<RefreshTokenService>();

// DB 기반 플레이어 인증 서비스
builder.Services.AddScoped<PlayerService>();

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

// DB 자동 마이그레이션 (개발 편의용 — 운영에서는 스크립트로 직접 적용)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DbWebAppContext>();
    await db.Database.MigrateAsync();
}

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
