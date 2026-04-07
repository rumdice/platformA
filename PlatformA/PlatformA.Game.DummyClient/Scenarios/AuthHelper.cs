using PlatformA.Library.Common;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PlatformA.Game.DummyClient.Scenarios
{
    /// <summary>
    /// 로그인/토큰 갱신 공통 헬퍼. 모든 시나리오에서 재사용합니다.
    /// </summary>
    public record TokenSession(string AccessToken, string RefreshToken, int PlayerId);

    public static class AuthHelper
    {
        /// <summary>
        /// Auth.API 로그인. 성공 시 TokenSession 반환, 실패 시 null.
        /// </summary>
        public static async Task<TokenSession?> LoginAsync(
            HttpClient http, string username, string password)
        {
            try
            {
                var resp = await http.PostAsJsonAsync(
                    Consts.AUTH_API_URL,
                    new { Username = username, Password = password });

                if (!resp.IsSuccessStatusCode) return null;

                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                var root = doc.RootElement;
                return new TokenSession(
                    AccessToken:  root.GetProperty("token").GetString()!,
                    RefreshToken: root.GetProperty("refreshToken").GetString()!,
                    PlayerId:     root.GetProperty("playerId").GetInt32());
            }
            catch { return null; }
        }

        /// <summary>
        /// Refresh Token으로 새 Access Token + 새 Refresh Token 발급.
        /// 토큰 로테이션이 적용되므로 호출 후 기존 session은 폐기됩니다.
        /// 실패(만료 또는 서버 오류) 시 null 반환.
        /// </summary>
        public static async Task<TokenSession?> TryRefreshAsync(
            HttpClient http, TokenSession session)
        {
            try
            {
                var resp = await http.PostAsJsonAsync(
                    Consts.AUTH_API_REFRESH_URL,
                    new { RefreshToken = session.RefreshToken });

                if (!resp.IsSuccessStatusCode) return null;

                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                var root = doc.RootElement;
                return new TokenSession(
                    AccessToken:  root.GetProperty("token").GetString()!,
                    RefreshToken: root.GetProperty("refreshToken").GetString()!,
                    PlayerId:     session.PlayerId);
            }
            catch { return null; }
        }

        /// <summary>
        /// HttpClient의 Authorization 헤더를 현재 세션의 Access Token으로 설정합니다.
        /// </summary>
        public static void ApplyToken(HttpClient http, TokenSession session)
        {
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", session.AccessToken);
        }
    }
}
