using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PlatformA.Library.Common;
using PlatformA.Matching.API.Services;
using PlatformA.MySqlDB.Lib.DBWebApp;
using PlatformA.MySqlDB.Lib.DBWebApp.Entities;
using PlatformA.Tests.Matching.API.Helpers;
using StackExchange.Redis;

namespace PlatformA.Tests.Matching.API
{
    [Collection("MatchingApiCollection")]
    public class GameMatchServiceRatingTests
    {
        private readonly MatchingTestWebAppFactory _factory;
        private readonly GameMatchService _matchService;
        private readonly IDbContextFactory<DbWebAppContext> _dbFactory;

        public GameMatchServiceRatingTests(MatchingTestWebAppFactory factory)
        {
            _factory = factory;
            // CreateClient을 한 번 호출해 WebApplication이 초기화되도록 보장
            _ = factory.CreateClient();
            _matchService = factory.Services.GetRequiredService<GameMatchService>();
            _dbFactory = factory.Services.GetRequiredService<IDbContextFactory<DbWebAppContext>>();
        }

        [Fact]
        public async Task GetPlayerRating_RedisHit_ReturnsRedisValue()
        {
            // Arrange: StringGetAsync("player:rating:50001") → "1350"
            _factory.MockRedisDb
                .Setup(x => x.StringGetAsync(
                    It.Is<RedisKey>(k => k == "player:rating:50001"),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisValue)"1350");

            try
            {
                // Act
                int result = await _matchService.GetPlayerRatingAsync(50001);

                // Assert
                Assert.Equal(1350, result);
            }
            finally
            {
                // Restore: 해당 키의 StringGetAsync를 Null로 복원
                _factory.MockRedisDb
                    .Setup(x => x.StringGetAsync(
                        It.Is<RedisKey>(k => k == "player:rating:50001"),
                        It.IsAny<CommandFlags>()))
                    .ReturnsAsync(RedisValue.Null);
            }
        }

        [Fact]
        public async Task GetPlayerRating_RedisMiss_DbHit_ReturnsDbValue()
        {
            // Arrange: StringGetAsync → RedisValue.Null (Moq 기본값 — 별도 setup 불필요)
            // DB 시드: PlayerRating { PlayerId = 50002, Rating = 1420.0 }
            await using (var db = _dbFactory.CreateDbContext())
            {
                db.Players.Add(new Player
                {
                    Id = 50002,
                    Username = "rating_player50002",
                    PasswordHash = "hash",
                    CreatedAt = DateTime.UtcNow
                });
                db.PlayerRatings.Add(new PlayerRating
                {
                    PlayerId = 50002,
                    Rating = 1420.0,
                    UpdatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            // Act
            int result = await _matchService.GetPlayerRatingAsync(50002);

            // Assert
            Assert.Equal(1420, result);
        }

        [Fact]
        public async Task GetPlayerRating_RedisMiss_DbHit_RecachesInRedis()
        {
            // Arrange: StringGetAsync → RedisValue.Null (기본값)
            // DB 시드: PlayerRating { PlayerId = 50003, Rating = 1420.0 }
            await using (var db = _dbFactory.CreateDbContext())
            {
                db.Players.Add(new Player
                {
                    Id = 50003,
                    Username = "rating_player50003",
                    PasswordHash = "hash",
                    CreatedAt = DateTime.UtcNow
                });
                db.PlayerRatings.Add(new PlayerRating
                {
                    PlayerId = 50003,
                    Rating = 1420.0,
                    UpdatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            // Act
            await _matchService.GetPlayerRatingAsync(50003);

            // Assert: MockRedisDb.Invocations에서 StringSetAsync 호출 확인
            // StackExchange.Redis IDatabaseAsync.StringSetAsync는 TimeSpan?가 아닌 Expiration 타입을 사용한다.
            // TimeSpan → Expiration은 암묵 변환되며, Expiration.ToString()은 "EX {seconds}" 형식을 반환한다.
            bool recacheCalled = _factory.MockRedisDb.Invocations
                .Any(invocation =>
                {
                    if (invocation.Method.Name != "StringSetAsync") return false;
                    if (invocation.Arguments.Count < 3) return false;
                    if (invocation.Arguments[0] is not RedisKey key) return false;
                    if (key.ToString() != "player:rating:50003") return false;
                    if (invocation.Arguments[1] is not RedisValue val) return false;
                    if (val.ToString() != "1420") return false;
                    // Expiration(1h) → ToString() == "EX 3600"
                    var expiryStr = invocation.Arguments[2]?.ToString() ?? string.Empty;
                    return expiryStr == "EX 3600";
                });

            Assert.True(recacheCalled,
                "GetPlayerRatingAsync should re-cache the DB value in Redis with key='player:rating:50003', value='1420', expiry=1h (EX 3600)");
        }

        [Fact]
        public async Task GetPlayerRating_RedisMiss_DbMiss_ReturnsDefault()
        {
            // Arrange: StringGetAsync → RedisValue.Null (기본값), DB에 50004 없음

            // Act
            int result = await _matchService.GetPlayerRatingAsync(50004);

            // Assert
            Assert.Equal(Consts.DEFAULT_PLAYER_RATING, result);
        }
    }
}
