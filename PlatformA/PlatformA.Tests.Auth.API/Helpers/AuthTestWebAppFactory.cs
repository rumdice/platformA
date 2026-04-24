using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Polly;
using PlatformA.Library.Core;
using PlatformA.Library.RateLimit;
using PlatformA.MySqlDB.Lib.DBWebApp;
using StackExchange.Redis;
using System.Reflection;

namespace PlatformA.Tests.Auth.API.Helpers;

public class AuthTestWebAppFactory : WebApplicationFactory<Program>
{
    public Mock<IConnectionMultiplexer> MockRedis { get; } = new();
    public Mock<IDatabase> MockRedisDb { get; } = new();

    public AuthTestWebAppFactory()
    {
        MockRedis
            .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(MockRedisDb.Object);

        // ScriptEvaluateAsync 기본값:
        //   - RedisRateLimiterService: (int)1 == 1 → 허용
        //   - RefreshTokenService.GetAndRevokeAsync: !IsNull → 토큰 유효
        MockRedisDb
            .Setup(x => x.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1L));

        // RefreshTokenService.SaveAsync 가 호출하는 StringSetAsync → 항상 성공
        MockRedisDb
            .Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // 1. IDbContextFactory<DbWebAppContext> → InMemory 교체
            //    AddDbContextFactory 내부에서 DbContextOptions<T>를 TryAddSingleton으로 등록하므로
            //    기존 MySQL Options를 먼저 제거하지 않으면 InMemory로 교체되지 않음
            var toRemove = services
                .Where(d => d.ServiceType == typeof(IDbContextFactory<DbWebAppContext>)
                         || d.ServiceType == typeof(DbWebAppContext)
                         || d.ServiceType == typeof(DbContextOptions<DbWebAppContext>))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            var dbName = $"auth_test_{Guid.NewGuid():N}";
            services.AddDbContextFactory<DbWebAppContext>(options =>
                options.UseInMemoryDatabase(dbName));

            // 2. RedisManager → Reflection으로 Mock IConnectionMultiplexer 주입
            //    RedisManager는 private 생성자 Singleton이므로 직접 서브클래싱 불가
            var redisDesc = services.SingleOrDefault(
                d => d.ServiceType == typeof(RedisManager));
            if (redisDesc != null) services.Remove(redisDesc);

            services.AddSingleton<RedisManager>(_ =>
            {
                var instance = RedisManager.Instance;
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var type = typeof(RedisManager);

                type.GetField("_redis", flags)!.SetValue(instance, MockRedis.Object);
                // ResiliencePipeline.Empty: 재시도/회로차단기 없이 직접 실행
                type.GetField("_pipeline", flags)!.SetValue(instance, ResiliencePipeline.Empty);

                return instance;
            });

            // 3. RedisRateLimiterService → 상한값 1000으로 재등록
            //    login 엔드포인트의 [RedisRateLimit] 필터가 테스트를 차단하지 않도록
            var rateLimiterDesc = services.SingleOrDefault(
                d => d.ServiceType == typeof(RedisRateLimiterService));
            if (rateLimiterDesc != null) services.Remove(rateLimiterDesc);

            services.AddSingleton<RedisRateLimiterService>(sp =>
            {
                var svc = new RedisRateLimiterService(sp.GetRequiredService<RedisManager>());
                svc.AddPolicy("login", permitLimit: 1000, window: TimeSpan.FromMinutes(1));
                return svc;
            });
        });

        builder.UseEnvironment("Testing");
    }
}
