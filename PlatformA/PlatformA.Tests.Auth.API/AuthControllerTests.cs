using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PlatformA.Auth.API.Models;
using PlatformA.Tests.Auth.API.Helpers;
using Xunit;

namespace PlatformA.Tests.Auth.API
{
    public class AuthControllerTests : IClassFixture<AuthTestWebAppFactory>
    {
        private readonly AuthTestWebAppFactory _factory;
        private readonly HttpClient _client;

        public AuthControllerTests(AuthTestWebAppFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        // ── Login ────────────────────────────────────────────────────────────

        [Fact]
        public async Task Login_NewUser_ValidCredentials_Returns200_WithTokens()
        {
            var response = await _client.PostAsJsonAsync("/api/auth/login",
                new { username = "newuser_test1", password = "pass1234" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.GetProperty("success").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("token").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("refreshToken").GetString()));
            Assert.True(json.GetProperty("playerId").GetInt32() > 0);
        }

        [Fact]
        public async Task Login_ExistingUser_WrongPassword_Returns401()
        {
            // 먼저 유저 등록 (자동 가입)
            await _client.PostAsJsonAsync("/api/auth/login",
                new { username = "wrongpw_user", password = "correct_pass123" });

            // 잘못된 비밀번호로 재시도
            var response = await _client.PostAsJsonAsync("/api/auth/login",
                new { username = "wrongpw_user", password = "wrong_pass" });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Login_ShortUsername_Returns400()
        {
            // MinLength(3) 위반: 2자 username
            var response = await _client.PostAsJsonAsync("/api/auth/login",
                new { username = "ab", password = "pass1234" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Login_ShortPassword_Returns400()
        {
            // MinLength(6) 위반: 5자 password
            var response = await _client.PostAsJsonAsync("/api/auth/login",
                new { username = "validuser", password = "pass" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ── Refresh ──────────────────────────────────────────────────────────

        [Fact]
        public async Task Refresh_ValidToken_Returns200_WithNewTokens()
        {
            // 로그인으로 실제 refresh token 획득
            var loginResp = await _client.PostAsJsonAsync("/api/auth/login",
                new { username = "refresh_user1", password = "pass1234" });
            loginResp.EnsureSuccessStatusCode();

            var loginJson = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
            var refreshToken = loginJson.GetProperty("refreshToken").GetString()!;

            // Refresh 요청
            var response = await _client.PostAsJsonAsync("/api/auth/refresh",
                new { refreshToken });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("token").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("refreshToken").GetString()));
        }

        [Fact]
        public async Task Refresh_MalformedToken_Returns401()
        {
            // 정수 prefix 없음 → ParsePlayerIdFromRefreshToken이 null 반환 → 즉시 401
            var response = await _client.PostAsJsonAsync("/api/auth/refresh",
                new { refreshToken = "invalid_token_no_int_prefix" });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // ── Logout ───────────────────────────────────────────────────────────

        [Fact]
        public async Task Logout_ValidToken_Returns200()
        {
            // 로그인으로 실제 refresh token 획득
            var loginResp = await _client.PostAsJsonAsync("/api/auth/login",
                new { username = "logout_user1", password = "pass1234" });
            loginResp.EnsureSuccessStatusCode();

            var loginJson = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
            var refreshToken = loginJson.GetProperty("refreshToken").GetString()!;

            // Logout 요청
            var response = await _client.PostAsJsonAsync("/api/auth/logout",
                new { refreshToken });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("message").GetString()));
        }

        [Fact]
        public async Task Logout_MalformedToken_Returns401()
        {
            // 정수 prefix 없음 → ParsePlayerIdFromRefreshToken이 null 반환 → 즉시 401
            var response = await _client.PostAsJsonAsync("/api/auth/logout",
                new { refreshToken = "invalid_token_no_int_prefix" });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
