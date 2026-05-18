using System.Net;
using System.Net.Http.Json;
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
    }
}
