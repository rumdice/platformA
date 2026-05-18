using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using PlatformA.Library.Core;
using PlatformA.Library.RateLimit;
using Polly;
using StackExchange.Redis;

namespace PlatformA.Tests.Ticketing.API.Helpers
{
    public class TicketingTestWebAppFactory : WebApplicationFactory<Program>
    {
        public Mock<IConnectionMultiplexer> MockRedis { get; } = new();
        public Mock<IDatabase> MockRedisDb { get; } = new();

        public TicketingTestWebAppFactory()
        {
            MockRedis
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(MockRedisDb.Object);

            // ScriptEvaluateAsync 기본값: 1L
            //   - RegisterQueueAsync (Lua ZADD): 1 = 대기열 진입 성공
            //   - LeaveQueueAsync (Lua ZREM): 1 = 제거 성공
            //   - UpdateHeartbeatAndGetRankAsync: 1 = rank 1 (대기 중)
            MockRedisDb
                .Setup(x => x.ScriptEvaluateAsync(
                    It.IsAny<string>(),
                    It.IsAny<RedisKey[]>(),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisResult.Create(1L));

            // SortedSetAddAsync (UpdateHeartbeatAsync → ZADD heartbeats)
            MockRedisDb
                .Setup(x => x.SortedSetAddAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<double>(),
                    It.IsAny<SortedSetWhen>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // KeyExistsAsync (IsActiveAsync → EXISTS active:user:{id}) — 기본 false (대기 중 상태)
            MockRedisDb
                .Setup(x => x.KeyExistsAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(false);

            // StringSetAsync
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
                // 1. RedisManager → Reflection으로 Mock IConnectionMultiplexer 주입
                var redisDesc = services.SingleOrDefault(
                    d => d.ServiceType == typeof(RedisManager));
                if (redisDesc != null)
                    services.Remove(redisDesc);

                services.AddSingleton<RedisManager>(_ =>
                {
                    var instance = RedisManager.Instance;
                    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                    var type = typeof(RedisManager);

                    type.GetField("_redis", flags)!.SetValue(instance, MockRedis.Object);
                    type.GetField("_pipeline", flags)!.SetValue(instance, ResiliencePipeline.Empty);

                    return instance;
                });

                // 2. RedisRateLimiterService → policy "queue" limit 1000으로 재등록
                //    QueueController의 [RedisRateLimit("queue")] 필터가 테스트를 차단하지 않도록
                var rateLimiterDesc = services.SingleOrDefault(
                    d => d.ServiceType == typeof(RedisRateLimiterService));
                if (rateLimiterDesc != null)
                    services.Remove(rateLimiterDesc);

                services.AddSingleton<RedisRateLimiterService>(sp =>
                {
                    var svc = new RedisRateLimiterService(sp.GetRequiredService<RedisManager>());
                    svc.AddPolicy("queue", permitLimit: 1000, window: TimeSpan.FromMinutes(1));
                    return svc;
                });

                // 3. IHostedService (QueueWorkerService) 제거
                var hostedServices = services
                    .Where(d => d.ServiceType == typeof(IHostedService))
                    .ToList();
                foreach (var d in hostedServices)
                    services.Remove(d);
            });

            builder.UseEnvironment("Testing");
        }
    }
}
