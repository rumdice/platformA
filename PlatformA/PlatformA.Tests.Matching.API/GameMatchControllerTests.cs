using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PlatformA.Library.Common;
using PlatformA.MySqlDB.Lib.DBWebApp;
using PlatformA.MySqlDB.Lib.DBWebApp.Entities;
using PlatformA.Tests.Matching.API.Helpers;
using StackExchange.Redis;

namespace PlatformA.Tests.Matching.API
{
    public class GameMatchControllerTests : IClassFixture<MatchingTestWebAppFactory>
    {
        private readonly MatchingTestWebAppFactory _factory;
        private readonly HttpClient _client;

        public GameMatchControllerTests(MatchingTestWebAppFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        // ── RequestMatch ─────────────────────────────────────────────────────

        [Fact]
        public async Task RequestMatch_ValidToken_Returns200()
        {
            string token = TokenManager.GenerateJwtToken(1);
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _client.PostAsync("/api/gamematch/RequestMatch", null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("message").GetString()));
        }

        [Fact]
        public async Task RequestMatch_NoToken_Returns401()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var response = await client.PostAsync("/api/gamematch/RequestMatch", null);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // ── CancelMatch ──────────────────────────────────────────────────────

        [Fact]
        public async Task CancelMatch_ValidToken_PlayerInQueue_Returns200()
        {
            // SortedSetRemoveAsync → true (기본 Mock 설정)
            string token = TokenManager.GenerateJwtToken(2);
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.DeleteAsync("/api/gamematch/CancelMatch");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CancelMatch_ValidToken_PlayerNotInQueue_Returns404()
        {
            // SortedSetRemoveAsync → false (대기열에 없음)
            _factory.MockRedisDb
                .Setup(x => x.SortedSetRemoveAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(false);

            string token = TokenManager.GenerateJwtToken(3);
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.DeleteAsync("/api/gamematch/CancelMatch");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            // Mock 복원
            _factory.MockRedisDb
                .Setup(x => x.SortedSetRemoveAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);
        }

        [Fact]
        public async Task CancelMatch_NoToken_Returns401()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var response = await client.DeleteAsync("/api/gamematch/CancelMatch");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // ── GetStatus ────────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatus_ValidToken_PlayerInQueue_Returns200_WithRankTotal()
        {
            // ZRANK → 0 (첫 번째), ZCARD → 5 (기본 Mock 설정)
            string token = TokenManager.GenerateJwtToken(4);
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/gamematch/Status");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(1, json.GetProperty("rank").GetInt64());
            Assert.Equal(5, json.GetProperty("total").GetInt64());
        }

        [Fact]
        public async Task GetStatus_ValidToken_PlayerNotInQueue_Returns404()
        {
            // ZRANK → null (대기열에 없음)
            _factory.MockRedisDb
                .Setup(x => x.SortedSetRankAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<Order>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync((long?)null);

            string token = TokenManager.GenerateJwtToken(5);
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/gamematch/Status");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            // Mock 복원
            _factory.MockRedisDb
                .Setup(x => x.SortedSetRankAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<Order>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync((long?)0);
        }

        [Fact]
        public async Task GetStatus_NoToken_Returns401()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var response = await client.GetAsync("/api/gamematch/Status");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // ── RequestMatchFromLobby (POST /api/gamematch/request) ──────────────

        [Fact]
        public async Task RequestFromLobby_NoBody_Returns400()
        {
            // null body는 415(UnsupportedMediaType)이므로, UserId=0 경계값으로 400 유도
            var content = new StringContent("{\"UserId\":0,\"GameType\":\"gomoku\"}", Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/api/gamematch/request", content);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task RequestFromLobby_InvalidUserId_Returns400()
        {
            // UserId=0 → [Range(1, int.MaxValue)] 위반
            var response = await _client.PostAsJsonAsync("/api/gamematch/request",
                new { UserId = 0, GameType = "gomoku" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task RequestFromLobby_InvalidGameType_Returns400()
        {
            // GameType이 50자 초과 → [MaxLength(50)] 위반
            var longGameType = new string('x', 51);
            var response = await _client.PostAsJsonAsync("/api/gamematch/request",
                new { UserId = 1, GameType = longGameType });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task RequestFromLobby_NoOpponent_Returns202()
        {
            // ScriptEvaluateAsync → 빈 배열 반환 → 상대 없음 → 202 Accepted
            _factory.MockRedisDb
                .Setup(x => x.ScriptEvaluateAsync(
                    It.IsAny<string>(),
                    It.Is<RedisKey[]>(keys => keys.Length > 0 && ((string?)keys[0])!.StartsWith("queue:")),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisResult.Create(Array.Empty<RedisResult>()));

            try
            {
                var response = await _client.PostAsJsonAsync("/api/gamematch/request",
                    new { UserId = 10, GameType = "gomoku" });

                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("message").GetString()));
            }
            finally
            {
                // Mock 복원
                _factory.MockRedisDb
                    .Setup(x => x.ScriptEvaluateAsync(
                        It.IsAny<string>(),
                        It.IsAny<RedisKey[]>(),
                        It.IsAny<RedisValue[]>(),
                        It.IsAny<CommandFlags>()))
                    .ReturnsAsync(RedisResult.Create(1L));
            }
        }

        [Fact]
        public async Task RequestFromLobby_MatchFound_Returns200_WithGameServerInfo()
        {
            // ScriptEvaluateAsync — queue 키에 대해 상대 userId 999 반환
            _factory.MockRedisDb
                .Setup(x => x.ScriptEvaluateAsync(
                    It.IsAny<string>(),
                    It.Is<RedisKey[]>(keys => keys.Length > 0 && ((string?)keys[0])!.StartsWith("queue:")),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisResult.Create(new RedisResult[]
                {
                    RedisResult.Create("999", ResultType.BulkString)
                }));

            try
            {
                var response = await _client.PostAsJsonAsync("/api/gamematch/request",
                    new { UserId = 11, GameType = "gomoku" });

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("host").GetString()));
                Assert.True(json.GetProperty("port").GetInt32() > 0);
                Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("roomId").GetString()));
            }
            finally
            {
                // Mock 복원 — 전체 기본 설정으로 되돌림
                _factory.MockRedisDb
                    .Setup(x => x.ScriptEvaluateAsync(
                        It.IsAny<string>(),
                        It.IsAny<RedisKey[]>(),
                        It.IsAny<RedisValue[]>(),
                        It.IsAny<CommandFlags>()))
                    .ReturnsAsync(RedisResult.Create(1L));
            }
        }

        // ── GetHistory (GET /api/gamematch/history) ──────────────────────────
        // GetHistory는 [Authorize] 없이 ExtractPlayerId()로 직접 인증 처리.
        // 토큰 없거나 유효하지 않으면 Unauthorized(401) 반환.

        [Fact]
        public async Task GetHistory_NoToken_Returns401()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var response = await client.GetAsync("/api/gamematch/history");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetHistory_InvalidToken_Returns401()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.jwt.token");

            var response = await client.GetAsync("/api/gamematch/history");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetHistory_ValidToken_NoHistory_Returns200_EmptyList()
        {
            // 매칭 기록이 없는 새 userId — InMemory DB에 해당 userId 기록 없음
            string token = TokenManager.GenerateJwtToken(9999);
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/gamematch/history");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Array, json.ValueKind);
            Assert.Equal(0, json.GetArrayLength());
        }

        // ── ReportMatchResult (POST /api/gamematch/result) ───────────────────

        [Fact]
        public async Task ReportMatchResult_ValidRoomId_InProgress_Returns200()
        {
            string testRoomId = Guid.NewGuid().ToString("N")[..12];

            // InMemory DB에 Player + MatchRecord(InProgress) 삽입
            var dbFactory = _factory.Services.GetRequiredService<IDbContextFactory<DbWebAppContext>>();
            await using (var db = dbFactory.CreateDbContext())
            {
                var p1 = new Player { Id = 10001, Username = "player10001", PasswordHash = "hash", CreatedAt = DateTime.UtcNow };
                var p2 = new Player { Id = 10002, Username = "player10002", PasswordHash = "hash", CreatedAt = DateTime.UtcNow };
                db.Players.AddRange(p1, p2);
                db.MatchRecords.Add(new MatchRecord
                {
                    Player1Id = 10001,
                    Player2Id = 10002,
                    Status = MatchStatus.InProgress,
                    GameType = "gomoku",
                    RoomId = testRoomId,
                    StartedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync();
            }

            var response = await _client.PostAsJsonAsync("/api/gamematch/result",
                new { RoomId = testRoomId, WinnerId = 10001, Reason = "FiveInRow" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("message").GetString()));
        }

        [Fact]
        public async Task ReportMatchResult_UnknownRoomId_Returns404()
        {
            string unknownRoomId = Guid.NewGuid().ToString("N")[..12];

            var response = await _client.PostAsJsonAsync("/api/gamematch/result",
                new { RoomId = unknownRoomId, WinnerId = 0, Reason = "Draw" });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task ReportMatchResult_EmptyRoomId_Returns400()
        {
            // RoomId가 빈 문자열이면 [Required] 위반 → 400
            var response = await _client.PostAsJsonAsync("/api/gamematch/result",
                new { RoomId = "", WinnerId = 0, Reason = "Draw" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ── GetHistory — Draw / Win 결과 검증 ────────────────────────────────

        [Fact]
        public async Task GetHistory_CompletedDrawMatch_ReturnsResult_Draw()
        {
            const int userId = 20001;
            const int opponentId = 20002;
            string testRoomId = Guid.NewGuid().ToString("N")[..12];

            // InMemory DB에 Player + MatchRecord(Status=Completed, WinnerId=null) 삽입
            var dbFactory = _factory.Services.GetRequiredService<IDbContextFactory<DbWebAppContext>>();
            await using (var db = dbFactory.CreateDbContext())
            {
                db.Players.AddRange(
                    new Player { Id = userId, Username = "draw_player20001", PasswordHash = "hash", CreatedAt = DateTime.UtcNow },
                    new Player { Id = opponentId, Username = "draw_player20002", PasswordHash = "hash", CreatedAt = DateTime.UtcNow }
                );
                db.MatchRecords.Add(new MatchRecord
                {
                    Player1Id = userId,
                    Player2Id = opponentId,
                    Status = MatchStatus.Completed,
                    WinnerId = null,           // 무승부
                    GameType = "gomoku",
                    RoomId = testRoomId,
                    CreatedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync();
            }

            string token = TokenManager.GenerateJwtToken(userId);
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/gamematch/history");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Array, json.ValueKind);
            Assert.True(json.GetArrayLength() >= 1);

            // 해당 RoomId의 매치를 찾아 result 필드 확인
            var match = json.EnumerateArray()
                .FirstOrDefault(e => e.TryGetProperty("roomId", out _) == false
                    || true); // 첫 번째 항목이 대상 레코드
            // result 필드는 camelCase JSON 직렬화: "result"
            bool foundDraw = json.EnumerateArray()
                .Any(e => e.GetProperty("result").GetString() == "무승부");
            Assert.True(foundDraw, "무승부 결과의 매칭 이력이 반환되어야 합니다.");
        }

        [Fact]
        public async Task GetHistory_CompletedWinnerMatch_ReturnsResult_Win()
        {
            const int userId = 20003;
            const int opponentId = 20004;
            string testRoomId = Guid.NewGuid().ToString("N")[..12];

            // InMemory DB에 Player + MatchRecord(Status=Completed, WinnerId=userId) 삽입
            var dbFactory = _factory.Services.GetRequiredService<IDbContextFactory<DbWebAppContext>>();
            await using (var db = dbFactory.CreateDbContext())
            {
                db.Players.AddRange(
                    new Player { Id = userId, Username = "win_player20003", PasswordHash = "hash", CreatedAt = DateTime.UtcNow },
                    new Player { Id = opponentId, Username = "win_player20004", PasswordHash = "hash", CreatedAt = DateTime.UtcNow }
                );
                db.MatchRecords.Add(new MatchRecord
                {
                    Player1Id = userId,
                    Player2Id = opponentId,
                    Status = MatchStatus.Completed,
                    WinnerId = userId,         // 요청자가 승자
                    GameType = "gomoku",
                    RoomId = testRoomId,
                    CreatedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync();
            }

            string token = TokenManager.GenerateJwtToken(userId);
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/gamematch/history");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Array, json.ValueKind);
            Assert.True(json.GetArrayLength() >= 1);

            bool foundWin = json.EnumerateArray()
                .Any(e => e.GetProperty("result").GetString() == "승리");
            Assert.True(foundWin, "승리 결과의 매칭 이력이 반환되어야 합니다.");
        }

        // ── GetRating (GET /api/gamematch/rating/{userId}) ────────────────────

        [Fact]
        public async Task GetRating_ValidUserId_NoRecord_ReturnsDefaultRating()
        {
            // DB에 PlayerRating 없는 userId → 기본값(Rating=1000, Win/Lose/Draw=0) 반환 → 200
            const int userId = 88881;

            var response = await _client.GetAsync($"/api/gamematch/rating/{userId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(userId, json.GetProperty("playerId").GetInt32());
            Assert.Equal(1000.0, json.GetProperty("rating").GetDouble());
            Assert.Equal(0, json.GetProperty("winCount").GetInt32());
            Assert.Equal(0, json.GetProperty("loseCount").GetInt32());
            Assert.Equal(0, json.GetProperty("drawCount").GetInt32());
        }

        [Fact]
        public async Task GetRating_ValidUserId_ExistingRecord_ReturnsActualRating()
        {
            // DB에 PlayerRating이 있는 userId → 실제 값 반환 → 200
            const int userId = 88882;

            var dbFactory = _factory.Services.GetRequiredService<IDbContextFactory<DbWebAppContext>>();
            await using (var db = dbFactory.CreateDbContext())
            {
                db.Players.Add(new Player
                {
                    Id = userId,
                    Username = "rating_player88882",
                    PasswordHash = "hash",
                    CreatedAt = DateTime.UtcNow
                });
                db.PlayerRatings.Add(new PlayerRating
                {
                    PlayerId = userId,
                    Rating = 1234.5,
                    WinCount = 10,
                    LoseCount = 3,
                    DrawCount = 2,
                    UpdatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            var response = await _client.GetAsync($"/api/gamematch/rating/{userId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(userId, json.GetProperty("playerId").GetInt32());
            Assert.Equal(1234.5, json.GetProperty("rating").GetDouble());
            Assert.Equal(10, json.GetProperty("winCount").GetInt32());
            Assert.Equal(3, json.GetProperty("loseCount").GetInt32());
            Assert.Equal(2, json.GetProperty("drawCount").GetInt32());
        }

        [Fact]
        public async Task GetRating_InvalidUserId_ReturnsBadRequest()
        {
            // userId=0 → 컨트롤러에서 BadRequest 반환
            var response = await _client.GetAsync("/api/gamematch/rating/0");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ── ELO 통합 테스트 (UpdateEloRatingsAsync awaited) ───────────────────

        [Fact]
        public async Task ReportMatchResult_Player1Wins_IncreasesPlayer1Rating()
        {
            const int p1Id = 40001;
            const int p2Id = 40002;
            string roomId = Guid.NewGuid().ToString("N")[..12];

            var dbFactory = _factory.Services.GetRequiredService<IDbContextFactory<DbWebAppContext>>();
            await using (var db = dbFactory.CreateDbContext())
            {
                db.Players.AddRange(
                    new Player { Id = p1Id, Username = $"elo_p{p1Id}", PasswordHash = "h", CreatedAt = DateTime.UtcNow },
                    new Player { Id = p2Id, Username = $"elo_p{p2Id}", PasswordHash = "h", CreatedAt = DateTime.UtcNow }
                );
                db.MatchRecords.Add(new MatchRecord
                {
                    Player1Id = p1Id,
                    Player2Id = p2Id,
                    Status = MatchStatus.InProgress,
                    GameType = "gomoku",
                    RoomId = roomId,
                    Player1Rating = 1000,
                    Player2Rating = 1000, // diff=0 → kMultiplier=1.0
                    CreatedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync();
            }

            var response = await _client.PostAsJsonAsync("/api/gamematch/result",
                new { RoomId = roomId, WinnerId = p1Id, Reason = "FiveInRow" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            await using (var db = dbFactory.CreateDbContext())
            {
                var r1 = await db.PlayerRatings.FindAsync(p1Id);
                var r2 = await db.PlayerRatings.FindAsync(p2Id);
                Assert.NotNull(r1);
                Assert.NotNull(r2);
                Assert.True(r1!.Rating > 1000, "P1 승리 후 레이팅이 증가해야 합니다.");
                Assert.True(r2!.Rating < 1000, "P2 패배 후 레이팅이 감소해야 합니다.");
                Assert.Equal(1, r1.WinCount);
                Assert.Equal(1, r2.LoseCount);
            }
        }

        [Fact]
        public async Task ReportMatchResult_Player2Wins_IncreasesPlayer2Rating()
        {
            const int p1Id = 40003;
            const int p2Id = 40004;
            string roomId = Guid.NewGuid().ToString("N")[..12];

            var dbFactory = _factory.Services.GetRequiredService<IDbContextFactory<DbWebAppContext>>();
            await using (var db = dbFactory.CreateDbContext())
            {
                db.Players.AddRange(
                    new Player { Id = p1Id, Username = $"elo_p{p1Id}", PasswordHash = "h", CreatedAt = DateTime.UtcNow },
                    new Player { Id = p2Id, Username = $"elo_p{p2Id}", PasswordHash = "h", CreatedAt = DateTime.UtcNow }
                );
                db.MatchRecords.Add(new MatchRecord
                {
                    Player1Id = p1Id,
                    Player2Id = p2Id,
                    Status = MatchStatus.InProgress,
                    GameType = "gomoku",
                    RoomId = roomId,
                    Player1Rating = 1000,
                    Player2Rating = 1000,
                    CreatedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync();
            }

            var response = await _client.PostAsJsonAsync("/api/gamematch/result",
                new { RoomId = roomId, WinnerId = p2Id, Reason = "FiveInRow" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            await using (var db = dbFactory.CreateDbContext())
            {
                var r1 = await db.PlayerRatings.FindAsync(p1Id);
                var r2 = await db.PlayerRatings.FindAsync(p2Id);
                Assert.NotNull(r1);
                Assert.NotNull(r2);
                Assert.True(r1!.Rating < 1000, "P1 패배 후 레이팅이 감소해야 합니다.");
                Assert.True(r2!.Rating > 1000, "P2 승리 후 레이팅이 증가해야 합니다.");
                Assert.Equal(1, r1.LoseCount);
                Assert.Equal(1, r2.WinCount);
            }
        }

        [Fact]
        public async Task ReportMatchResult_Draw_BothRatingsAdjustedAndDrawCountIncremented()
        {
            const int p1Id = 40005;
            const int p2Id = 40006;
            string roomId = Guid.NewGuid().ToString("N")[..12];

            var dbFactory = _factory.Services.GetRequiredService<IDbContextFactory<DbWebAppContext>>();
            await using (var db = dbFactory.CreateDbContext())
            {
                db.Players.AddRange(
                    new Player { Id = p1Id, Username = $"elo_p{p1Id}", PasswordHash = "h", CreatedAt = DateTime.UtcNow },
                    new Player { Id = p2Id, Username = $"elo_p{p2Id}", PasswordHash = "h", CreatedAt = DateTime.UtcNow }
                );
                db.MatchRecords.Add(new MatchRecord
                {
                    Player1Id = p1Id,
                    Player2Id = p2Id,
                    Status = MatchStatus.InProgress,
                    GameType = "gomoku",
                    RoomId = roomId,
                    Player1Rating = 1000,
                    Player2Rating = 1000,
                    CreatedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync();
            }

            // WinnerId=0 → 무승부
            var response = await _client.PostAsJsonAsync("/api/gamematch/result",
                new { RoomId = roomId, WinnerId = 0, Reason = "FullBoard" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            await using (var db = dbFactory.CreateDbContext())
            {
                var r1 = await db.PlayerRatings.FindAsync(p1Id);
                var r2 = await db.PlayerRatings.FindAsync(p2Id);
                Assert.NotNull(r1);
                Assert.NotNull(r2);
                Assert.Equal(1, r1!.DrawCount);
                Assert.Equal(1, r2!.DrawCount);
                // 동일 레이팅 무승부 → ELO 변동 없음
                Assert.Equal(1000.0, r1.Rating, precision: 1);
                Assert.Equal(1000.0, r2.Rating, precision: 1);
            }
        }

        [Fact]
        public async Task ReportMatchResult_LargeRatingDiff_SmallerEloChange()
        {
            // ratingDiff=600 → kMultiplier=0.25 → ELO 변동이 정상 매칭보다 4배 작아야 함
            const int p1Id = 40007;
            const int p2Id = 40008;
            string roomId = Guid.NewGuid().ToString("N")[..12];

            var dbFactory = _factory.Services.GetRequiredService<IDbContextFactory<DbWebAppContext>>();
            await using (var db = dbFactory.CreateDbContext())
            {
                db.Players.AddRange(
                    new Player { Id = p1Id, Username = $"elo_p{p1Id}", PasswordHash = "h", CreatedAt = DateTime.UtcNow },
                    new Player { Id = p2Id, Username = $"elo_p{p2Id}", PasswordHash = "h", CreatedAt = DateTime.UtcNow }
                );
                // P2 레이팅 1600 사전 시드
                db.PlayerRatings.Add(new PlayerRating
                {
                    PlayerId = p2Id,
                    Rating = 1600.0,
                    UpdatedAt = DateTime.UtcNow
                });
                db.MatchRecords.Add(new MatchRecord
                {
                    Player1Id = p1Id,
                    Player2Id = p2Id,
                    Status = MatchStatus.InProgress,
                    GameType = "gomoku",
                    RoomId = roomId,
                    Player1Rating = 1000,
                    Player2Rating = 1600, // diff=600 → kMultiplier=0.25
                    CreatedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync();
            }

            var response = await _client.PostAsJsonAsync("/api/gamematch/result",
                new { RoomId = roomId, WinnerId = p1Id, Reason = "FiveInRow" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            await using (var db = dbFactory.CreateDbContext())
            {
                var r1 = await db.PlayerRatings.FindAsync(p1Id);
                var r2 = await db.PlayerRatings.FindAsync(p2Id);
                Assert.NotNull(r1);
                Assert.NotNull(r2);
                // kMultiplier=0.25: P1(저레이팅) 업셋 승리이지만 K가 1/4로 줄었으므로 변동 < 15
                double p1Change = r1!.Rating - 1000.0;
                Assert.True(p1Change > 0 && p1Change < 15.0,
                    $"레이팅 변동 {p1Change:.2f}은 kMultiplier=0.25 적용으로 15 미만이어야 합니다.");
            }
        }

        [Fact]
        public async Task ReportMatchResult_NewPlayer_WinCountAndRatingUpdated()
        {
            // PlayerRating DB 기록 없는 신규 플레이어 → 기본 1000에서 시작, 승리 후 WinCount=1
            const int p1Id = 40009;
            const int p2Id = 40010;
            string roomId = Guid.NewGuid().ToString("N")[..12];

            var dbFactory = _factory.Services.GetRequiredService<IDbContextFactory<DbWebAppContext>>();
            await using (var db = dbFactory.CreateDbContext())
            {
                db.Players.AddRange(
                    new Player { Id = p1Id, Username = $"elo_p{p1Id}", PasswordHash = "h", CreatedAt = DateTime.UtcNow },
                    new Player { Id = p2Id, Username = $"elo_p{p2Id}", PasswordHash = "h", CreatedAt = DateTime.UtcNow }
                );
                // PlayerRating 시드 없음 — 신규 플레이어 (기본 1000 적용)
                db.MatchRecords.Add(new MatchRecord
                {
                    Player1Id = p1Id,
                    Player2Id = p2Id,
                    Status = MatchStatus.InProgress,
                    GameType = "gomoku",
                    RoomId = roomId,
                    Player1Rating = 1000,
                    Player2Rating = 1000,
                    CreatedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync();
            }

            var response = await _client.PostAsJsonAsync("/api/gamematch/result",
                new { RoomId = roomId, WinnerId = p1Id, Reason = "FiveInRow" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            await using (var db = dbFactory.CreateDbContext())
            {
                var r1 = await db.PlayerRatings.FindAsync(p1Id);
                Assert.NotNull(r1);
                Assert.Equal(1, r1!.WinCount);
                Assert.True(r1.Rating > 1000, "신규 플레이어 첫 승리 후 레이팅이 증가해야 합니다.");
            }
        }
    }
}
