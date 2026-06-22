using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;
using PlatformA.Library.Common;
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
    }
}
