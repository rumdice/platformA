using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlatformA.Tests.Utils.API.Helpers;
using PlatformA.Utils.API;
using PlatformA.Utils.API.Models;

namespace PlatformA.Tests.Utils.API;

public class UtilControllerTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebAppFactory _factory;

    public UtilControllerTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false   // 302 리다이렉트를 직접 검증하기 위해
        });
    }

    // ─────────────────────────────────────────────
    // GET /util/myip
    // ─────────────────────────────────────────────

    [Fact]
    public async Task GetMyIp_Returns200_WithIpField()
    {
        var response = await _client.GetAsync("/util/myip");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("ip", out _), "응답에 'ip' 필드가 없습니다.");
    }

    // ─────────────────────────────────────────────
    // POST /util/shorten
    // ─────────────────────────────────────────────

    [Fact]
    public async Task ShortenUrl_ValidUrl_Returns200_WithShortUrlAndCode()
    {
        var response = await _client.PostAsJsonAsync("/util/shorten",
            new UrlRequestDto { Url = "https://github.com/rumdice/platformA" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("shortUrl", out var shortUrl), "응답에 'shortUrl' 필드가 없습니다.");
        Assert.True(json.TryGetProperty("code", out var code), "응답에 'code' 필드가 없습니다.");
        Assert.False(string.IsNullOrEmpty(shortUrl.GetString()));
        Assert.False(string.IsNullOrEmpty(code.GetString()));
    }

    [Fact]
    public async Task ShortenUrl_InvalidUrl_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/util/shorten",
            new UrlRequestDto { Url = "not-a-valid-url" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ShortenUrl_EmptyUrl_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/util/shorten",
            new UrlRequestDto { Url = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─────────────────────────────────────────────
    // GET /go/{code}
    // ─────────────────────────────────────────────

    [Fact]
    public async Task RedirectUrl_UnknownCode_Returns404()
    {
        var response = await _client.GetAsync("/go/unknownCode999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RedirectUrl_KnownCode_Returns302_ToOriginalUrl()
    {
        const string originalUrl = "https://example.com/redirect-test";

        // 1. 단축 URL 생성
        var shortenResponse = await _client.PostAsJsonAsync("/util/shorten",
            new UrlRequestDto { Url = originalUrl });
        Assert.Equal(HttpStatusCode.OK, shortenResponse.StatusCode);

        var json = await shortenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var code = json.GetProperty("code").GetString()!;

        // 2. 리다이렉트 검증
        var redirectResponse = await _client.GetAsync($"/go/{code}");
        Assert.Equal(HttpStatusCode.Redirect, redirectResponse.StatusCode);
        Assert.Equal(originalUrl, redirectResponse.Headers.Location?.ToString());
    }

    // ─────────────────────────────────────────────
    // GET /util/stats/{code}
    // ─────────────────────────────────────────────

    [Fact]
    public async Task GetStats_UnknownCode_Returns404()
    {
        var response = await _client.GetAsync("/util/stats/unknownCode999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_KnownCode_Returns200_WithExpectedFields()
    {
        const string originalUrl = "https://example.com/stats-test";

        // 1. 단축 URL 생성
        var shortenResponse = await _client.PostAsJsonAsync("/util/shorten",
            new UrlRequestDto { Url = originalUrl });
        Assert.Equal(HttpStatusCode.OK, shortenResponse.StatusCode);

        var json = await shortenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var code = json.GetProperty("code").GetString()!;

        // 2. 통계 조회
        var statsResponse = await _client.GetAsync($"/util/stats/{code}");
        Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);

        var statsJson = await statsResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(statsJson.TryGetProperty("code", out _), "응답에 'code' 필드 없음");
        Assert.True(statsJson.TryGetProperty("originalUrl", out var url), "응답에 'originalUrl' 필드 없음");
        Assert.True(statsJson.TryGetProperty("clickCount", out _), "응답에 'clickCount' 필드 없음");
        Assert.Equal(originalUrl, url.GetString());
    }

    [Fact]
    public async Task ShortenUrl_TwiceSameUrl_ReturnsDifferentCodes()
    {
        const string url = "https://example.com/duplicate-test";

        var r1 = await _client.PostAsJsonAsync("/util/shorten", new UrlRequestDto { Url = url });
        var r2 = await _client.PostAsJsonAsync("/util/shorten", new UrlRequestDto { Url = url });

        var code1 = (await r1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
        var code2 = (await r2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();

        // Snowflake ID 기반이므로 같은 URL이라도 코드가 달라야 함
        Assert.NotEqual(code1, code2);
    }
}
