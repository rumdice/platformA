using System.Net;
using System.Text.Json;
using PlatformA.Tests.Utils.API.Helpers;

namespace PlatformA.Tests.Utils.API
{
    public class HealthCheckTests : IClassFixture<TestWebAppFactory>
    {
        private readonly HttpClient _client;

        public HealthCheckTests(TestWebAppFactory factory)
        {
            _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        // ─────────────────────────────────────────────
        // GET /healthz  (Liveness)
        // ─────────────────────────────────────────────

        [Fact]
        public async Task GetHealthz_Returns200()
        {
            var response = await _client.GetAsync("/healthz");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // ─────────────────────────────────────────────
        // GET /readyz  (Readiness)
        // ─────────────────────────────────────────────

        [Fact]
        public async Task GetReadyz_ReturnsJsonWithStatusField()
        {
            // /readyz 는 Redis Mock 이 AddRedis() 내부 연결을 대체하지 못하므로
            // 실제 Redis 없는 테스트 환경에서 503 ServiceUnavailable 이 반환될 수 있다.
            // 상태 코드가 아니라 응답 본문의 JSON 형식만 검증한다.
            var response = await _client.GetAsync("/readyz");

            Assert.True(
                response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.ServiceUnavailable,
                $"Unexpected status code: {response.StatusCode}");

            var body = await response.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrWhiteSpace(body), "응답 본문이 비어 있습니다.");

            var json = JsonDocument.Parse(body);
            Assert.True(json.RootElement.TryGetProperty("status", out _),
                "응답 JSON에 'status' 필드가 없습니다.");
        }

        [Fact]
        public async Task GetReadyz_ReturnsJsonWithChecksField()
        {
            var response = await _client.GetAsync("/readyz");

            Assert.True(
                response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.ServiceUnavailable,
                $"Unexpected status code: {response.StatusCode}");

            var body = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(body);

            Assert.True(json.RootElement.TryGetProperty("checks", out var checks),
                "응답 JSON에 'checks' 필드가 없습니다.");
            Assert.Equal(JsonValueKind.Object, checks.ValueKind);
        }
    }
}
