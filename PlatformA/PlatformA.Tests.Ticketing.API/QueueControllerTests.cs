using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;
using PlatformA.Library.Common;
using PlatformA.Tests.Ticketing.API.Helpers;
using StackExchange.Redis;

namespace PlatformA.Tests.Ticketing.API
{
    public class QueueControllerTests : IClassFixture<TicketingTestWebAppFactory>
    {
        private readonly TicketingTestWebAppFactory _factory;
        private readonly HttpClient _client;

        public QueueControllerTests(TicketingTestWebAppFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        private HttpClient CreateAuthenticatedClient(int playerId)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            string token = TokenManager.GenerateJwtToken(playerId);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        // ── EnterQueue ───────────────────────────────────────────────────────

        [Fact]
        public async Task EnterQueue_ValidToken_Returns200()
        {
            // ScriptEvaluateAsync → 1L (대기열 진입 성공)
            var client = CreateAuthenticatedClient(1);

            var response = await client.PostAsync("/api/queue/enter", null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task EnterQueue_NoToken_Returns401()
        {
            var response = await _client.PostAsync("/api/queue/enter", null);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // ── GetStatus ────────────────────────────────────────────────────────

        [Fact]
        public async Task GetStatus_ValidToken_Waiting_Returns200_WithRank()
        {
            // KeyExistsAsync → false (기본값, 대기 중)
            // ScriptEvaluateAsync → 1L → UpdateHeartbeatAndGetRankAsync 반환 rank=1
            var client = CreateAuthenticatedClient(2);

            var response = await client.GetAsync("/api/queue/status");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Waiting", json.GetProperty("status").GetString());
            Assert.True(json.GetProperty("rank").GetInt64() > 0);
        }

        [Fact]
        public async Task GetStatus_ValidToken_Active_Returns200()
        {
            // KeyExistsAsync → true (입장권 발급된 상태)
            _factory.MockRedisDb
                .Setup(x => x.KeyExistsAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            try
            {
                var client = CreateAuthenticatedClient(3);

                var response = await client.GetAsync("/api/queue/status");

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("Active", json.GetProperty("status").GetString());
            }
            finally
            {
                _factory.MockRedisDb
                    .Setup(x => x.KeyExistsAsync(
                        It.IsAny<RedisKey>(),
                        It.IsAny<CommandFlags>()))
                    .ReturnsAsync(false);
            }
        }

        [Fact]
        public async Task GetStatus_ValidToken_NotFound_Returns404()
        {
            // rate limit 키(rl:*)는 제외하고 큐 서비스 키만 -1L 반환
            // → UpdateHeartbeatAndGetRankAsync returns null → 404
            _factory.MockRedisDb
                .Setup(x => x.ScriptEvaluateAsync(
                    It.IsAny<string>(),
                    It.Is<RedisKey[]>(keys => keys != null && keys.Length > 0
                        && !((string)keys[0]).StartsWith("rl:")),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisResult.Create(-1L));

            try
            {
                var client = CreateAuthenticatedClient(4);

                var response = await client.GetAsync("/api/queue/status");

                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
            finally
            {
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
        public async Task GetStatus_NoToken_Returns401()
        {
            var response = await _client.GetAsync("/api/queue/status");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // ── LeaveQueue ───────────────────────────────────────────────────────

        [Fact]
        public async Task LeaveQueue_ValidToken_InQueue_Returns200()
        {
            // ScriptEvaluateAsync → 1L → LeaveQueueAsync returns true → 200
            var client = CreateAuthenticatedClient(5);

            var response = await client.PostAsync("/api/queue/leave", null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task LeaveQueue_ValidToken_NotInQueue_Returns404()
        {
            // rate limit 키(rl:*)는 제외하고 큐 서비스 키만 0L 반환
            // → LeaveQueueAsync returns false, IsActiveAsync = false → 404
            _factory.MockRedisDb
                .Setup(x => x.ScriptEvaluateAsync(
                    It.IsAny<string>(),
                    It.Is<RedisKey[]>(keys => keys != null && keys.Length > 0
                        && !((string)keys[0]).StartsWith("rl:")),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisResult.Create(0L));

            try
            {
                var client = CreateAuthenticatedClient(6);

                var response = await client.PostAsync("/api/queue/leave", null);

                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
            finally
            {
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
        public async Task LeaveQueue_NoToken_Returns401()
        {
            var response = await _client.PostAsync("/api/queue/leave", null);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // ── 추가 케이스 (Sprint #78) ─────────────────────────────────────────

        [Fact]
        public async Task EnterQueue_InvalidToken_Returns401()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.jwt.token");

            var response = await client.PostAsync("/api/queue/enter", null);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task EnterQueue_QueueFull_Returns400()
        {
            _factory.MockRedisDb
                .Setup(x => x.ScriptEvaluateAsync(
                    It.IsAny<string>(),
                    It.Is<RedisKey[]>(keys => keys != null && keys.Length > 0
                        && !((string)keys[0]).StartsWith("rl:")),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisResult.Create(-1L));

            try
            {
                var client = CreateAuthenticatedClient(10);

                var response = await client.PostAsync("/api/queue/enter", null);

                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
            finally
            {
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
        public async Task GetStatus_HighRank_Returns10000msDelay()
        {
            // rank 200 > 100 → CalculateSmartPollDelay = 10000
            _factory.MockRedisDb
                .Setup(x => x.ScriptEvaluateAsync(
                    It.IsAny<string>(),
                    It.Is<RedisKey[]>(keys => keys != null && keys.Length > 0
                        && !((string)keys[0]).StartsWith("rl:")),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisResult.Create(200L));

            try
            {
                var client = CreateAuthenticatedClient(11);

                var response = await client.GetAsync("/api/queue/status");

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("Waiting", json.GetProperty("status").GetString());
                Assert.Equal(10000, json.GetProperty("nextPollDelay").GetInt32());
            }
            finally
            {
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
        public async Task GetStatus_MidRank_Returns5000msDelay()
        {
            // rank 75 > 50 → CalculateSmartPollDelay = 5000
            _factory.MockRedisDb
                .Setup(x => x.ScriptEvaluateAsync(
                    It.IsAny<string>(),
                    It.Is<RedisKey[]>(keys => keys != null && keys.Length > 0
                        && !((string)keys[0]).StartsWith("rl:")),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisResult.Create(75L));

            try
            {
                var client = CreateAuthenticatedClient(12);

                var response = await client.GetAsync("/api/queue/status");

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("Waiting", json.GetProperty("status").GetString());
                Assert.Equal(5000, json.GetProperty("nextPollDelay").GetInt32());
            }
            finally
            {
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
        public async Task GetStatus_LowRank_Returns3000msDelay()
        {
            // rank 30 > 10 → CalculateSmartPollDelay = 3000
            _factory.MockRedisDb
                .Setup(x => x.ScriptEvaluateAsync(
                    It.IsAny<string>(),
                    It.Is<RedisKey[]>(keys => keys != null && keys.Length > 0
                        && !((string)keys[0]).StartsWith("rl:")),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisResult.Create(30L));

            try
            {
                var client = CreateAuthenticatedClient(13);

                var response = await client.GetAsync("/api/queue/status");

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("Waiting", json.GetProperty("status").GetString());
                Assert.Equal(3000, json.GetProperty("nextPollDelay").GetInt32());
            }
            finally
            {
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
        public async Task LeaveQueue_ActiveTicket_Returns400()
        {
            // LeaveQueueAsync → false (ScriptEvaluateAsync non-rl → 0L)
            // IsActiveAsync → true (KeyExistsAsync → true) → 400
            _factory.MockRedisDb
                .Setup(x => x.ScriptEvaluateAsync(
                    It.IsAny<string>(),
                    It.Is<RedisKey[]>(keys => keys != null && keys.Length > 0
                        && !((string)keys[0]).StartsWith("rl:")),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisResult.Create(0L));

            _factory.MockRedisDb
                .Setup(x => x.KeyExistsAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            try
            {
                var client = CreateAuthenticatedClient(14);

                var response = await client.PostAsync("/api/queue/leave", null);

                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
            finally
            {
                _factory.MockRedisDb
                    .Setup(x => x.ScriptEvaluateAsync(
                        It.IsAny<string>(),
                        It.IsAny<RedisKey[]>(),
                        It.IsAny<RedisValue[]>(),
                        It.IsAny<CommandFlags>()))
                    .ReturnsAsync(RedisResult.Create(1L));

                _factory.MockRedisDb
                    .Setup(x => x.KeyExistsAsync(
                        It.IsAny<RedisKey>(),
                        It.IsAny<CommandFlags>()))
                    .ReturnsAsync(false);
            }
        }

        [Fact]
        public async Task LeaveQueue_InvalidToken_Returns401()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.jwt.token");

            var response = await client.PostAsync("/api/queue/leave", null);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task EnterQueue_RateLimitExceeded_Returns429()
        {
            // rl:queue:{ip} 키에 대해서만 0L 반환 → [RedisRateLimit("queue")] 차단 → 429
            _factory.MockRedisDb
                .Setup(x => x.ScriptEvaluateAsync(
                    It.IsAny<string>(),
                    It.Is<RedisKey[]>(keys => keys != null && keys.Length > 0
                        && ((string)keys[0]).StartsWith("rl:queue:")),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisResult.Create(0L));

            try
            {
                var client = CreateAuthenticatedClient(15);

                var response = await client.PostAsync("/api/queue/enter", null);

                Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            }
            finally
            {
                _factory.MockRedisDb
                    .Setup(x => x.ScriptEvaluateAsync(
                        It.IsAny<string>(),
                        It.Is<RedisKey[]>(keys => keys != null && keys.Length > 0
                            && ((string)keys[0]).StartsWith("rl:queue:")),
                        It.IsAny<RedisValue[]>(),
                        It.IsAny<CommandFlags>()))
                    .ReturnsAsync(RedisResult.Create(1L));
            }
        }
    }
}
