using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using PlatformA.Library.Core;
using PlatformA.Utils.API;
using PlatformA.Utils.API.Services;
using StackExchange.Redis;

namespace PlatformA.Tests.Utils.API.Helpers
{
    public class TestWebAppFactory : WebApplicationFactory<Program>
    {
        // 테스트에서 Redis 목업에 접근할 수 있도록 공개
        public Mock<IConnectionMultiplexer> MockRedis { get; } = new();
        public Mock<IDatabase> MockRedisDb { get; } = new();

        public TestWebAppFactory()
        {
            // GetDatabase() 호출 시 목업 IDatabase 반환
            MockRedis
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(MockRedisDb.Object);

            // 캐시 미스 기본값: 빈 RedisValue 반환
            MockRedisDb
                .Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);

            // 쓰기 작업 기본값: 항상 성공
            MockRedisDb
                .Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            MockRedisDb
                .Setup(x => x.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(1L);

            MockRedisDb
                .Setup(x => x.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // 1. 실제 SQLite → 인메모리 SQLite로 교체
                var dbDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (dbDescriptor != null)
                    services.Remove(dbDescriptor);

                // 테스트마다 독립적인 DB (이름을 고유하게)
                var dbName = $"test_{Guid.NewGuid():N}";
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlite($"Data Source={dbName}.db"));

                // 2. 실제 RedisManager 제거
                var redisManagerDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(RedisManager));
                if (redisManagerDescriptor != null)
                    services.Remove(redisManagerDescriptor);

                // 3. 실제 IConnectionMultiplexer 제거 후 목업 등록
                var muxDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IConnectionMultiplexer));
                if (muxDescriptor != null)
                    services.Remove(muxDescriptor);
                services.AddSingleton(MockRedis.Object);

                // 4. StatSyncsService 제거 (Redis 실제 연결 시도 차단)
                var hostedDescriptors = services
                    .Where(d => d.ServiceType == typeof(IHostedService))
                    .ToList();
                foreach (var d in hostedDescriptors)
                    services.Remove(d);
            });

            builder.UseEnvironment("Testing");
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            return host;
        }
    }
}
