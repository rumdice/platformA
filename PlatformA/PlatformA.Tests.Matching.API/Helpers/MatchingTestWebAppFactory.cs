using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using PlatformA.Library.Core;
using PlatformA.MySqlDB.Lib.DBWebApp;
using Polly;
using StackExchange.Redis;

namespace PlatformA.Tests.Matching.API.Helpers
{
    public class MatchingTestWebAppFactory : WebApplicationFactory<Program>
    {
        public Mock<IConnectionMultiplexer> MockRedis { get; } = new();
        public Mock<IDatabase> MockRedisDb { get; } = new();

        public MatchingTestWebAppFactory()
        {
            MockRedis
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(MockRedisDb.Object);

            // ScriptEvaluateAsync: Rate Limit 통과 (1 반환)
            MockRedisDb
                .Setup(x => x.ScriptEvaluateAsync(
                    It.IsAny<string>(),
                    It.IsAny<RedisKey[]>(),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisResult.Create(1L));

            // SortedSetAddAsync (RequestMatch → ZADD 매칭 대기열 진입)
            MockRedisDb
                .Setup(x => x.SortedSetAddAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<double>(),
                    It.IsAny<SortedSetWhen>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // SortedSetRemoveAsync (CancelMatch → ZREM 대기열 제거) — 기본값 true
            MockRedisDb
                .Setup(x => x.SortedSetRemoveAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // SortedSetRankAsync (GetStatus → ZRANK) — 기본값: rank 0 (대기 중)
            MockRedisDb
                .Setup(x => x.SortedSetRankAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<Order>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync((long?)0);

            // SortedSetLengthAsync (GetStatus → ZCARD) — 기본값: 5명 대기 중
            MockRedisDb
                .Setup(x => x.SortedSetLengthAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<double>(),
                    It.IsAny<double>(),
                    It.IsAny<Exclude>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync((long)5);

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
                // 1. IDbContextFactory<DbWebAppContext> → InMemory 교체
                //    EF Core: AddDbContextFactory(InMemory)를 사용하면 DI에 InMemory IDatabaseProvider가
                //    추가되어 Pomelo IDatabaseProvider와 충돌(InvalidOperationException) 발생.
                //    대신 팩토리를 직접 등록해 InMemory 서비스가 DI 컨테이너에 들어가지 않게 한다.
                var toRemove = services
                    .Where(d => d.ServiceType == typeof(IDbContextFactory<DbWebAppContext>)
                             || d.ServiceType == typeof(DbWebAppContext)
                             || d.ServiceType == typeof(DbContextOptions<DbWebAppContext>))
                    .ToList();
                foreach (var d in toRemove)
                    services.Remove(d);

                var dbName = $"matching_test_{Guid.NewGuid():N}";
                var inMemoryOptions = new DbContextOptionsBuilder<DbWebAppContext>()
                    .UseInMemoryDatabase(dbName)
                    .Options;
                services.AddSingleton<IDbContextFactory<DbWebAppContext>>(
                    new InMemoryDbContextFactory(inMemoryOptions));

                // 2. RedisManager → Reflection으로 Mock IConnectionMultiplexer 주입
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

                // 3. IHostedService 전체 제거 (EngineService, GameMatchService의 실제 Redis 연결 차단)
                var hostedServices = services
                    .Where(d => d.ServiceType == typeof(IHostedService))
                    .ToList();
                foreach (var d in hostedServices)
                    services.Remove(d);
            });

            builder.UseEnvironment("Testing");
        }

        private sealed class InMemoryDbContextFactory : IDbContextFactory<DbWebAppContext>
        {
            private readonly DbContextOptions<DbWebAppContext> _options;
            public InMemoryDbContextFactory(DbContextOptions<DbWebAppContext> options) => _options = options;
            public DbWebAppContext CreateDbContext() => new(_options);
        }
    }
}
