using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using PlatformA.Library.Common;

namespace PlatformA.Game.DummyClient.Scenarios
{
    public class QueueStatusDto
    {
        public int UserId { get; set; }
        public long Rank { get; set; }
        public string Status { get; set; } = "";
        public int NextPollDelay { get; set; }
    }

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

                if (!resp.IsSuccessStatusCode)
                    return null;

                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                var root = doc.RootElement;
                return new TokenSession(
                    AccessToken: root.GetProperty("token").GetString()!,
                    RefreshToken: root.GetProperty("refreshToken").GetString()!,
                    PlayerId: root.GetProperty("playerId").GetInt32());
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

                if (!resp.IsSuccessStatusCode)
                    return null;

                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                var root = doc.RootElement;
                return new TokenSession(
                    AccessToken: root.GetProperty("token").GetString()!,
                    RefreshToken: root.GetProperty("refreshToken").GetString()!,
                    PlayerId: session.PlayerId);
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

        public static string GenerateTestUserName()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 8).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        /// <summary>
        /// Ticketing 대기열이 Active 될 때까지 대기합니다.
        /// SignalR QueueActivated push를 우선하고, 연결 실패 시 HTTP 폴링으로 fallback합니다.
        /// 대기 중 Access Token 만료 시 Refresh Token으로 자동 갱신합니다.
        /// </summary>
        /// <returns>(Activated, 최신 Session, 토큰 갱신 횟수)</returns>
        public static async Task<(bool Activated, TokenSession Session, int RefreshCount)> WaitUntilActiveAsync(
            HttpClient http, TokenSession session, int timeoutSeconds = 300)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            int refreshCount = 0;

            var hub = new HubConnectionBuilder()
                .WithUrl($"{Consts.TICKET_API_URL}/hubs/queue", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(session.AccessToken);
                    options.HttpMessageHandlerFactory = _ =>
                        new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
                })
                .Build();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            hub.On("QueueActivated", () => tcs.TrySetResult(true));

            bool signalRConnected = false;
            try
            {
                await hub.StartAsync(timeout.Token);
                signalRConnected = true;
            }
            catch { /* SignalR 연결 실패 → fallback 폴링 */ }

            try
            {
                while (!timeout.IsCancellationRequested)
                {
                    if (signalRConnected)
                    {
                        var pollDelay = Task.Delay(10_000, timeout.Token);
                        var completed = await Task.WhenAny(tcs.Task, pollDelay);
                        if (completed == tcs.Task)
                            return (true, session, refreshCount);
                    }

                    var resp = await http.GetAsync(
                        $"{Consts.TICKET_API_URL}/api/queue/status", timeout.Token);

                    if (resp.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        var newSession = await TryRefreshAsync(http, session);
                        if (newSession == null)
                            return (false, session, refreshCount);
                        session = newSession;
                        ApplyToken(http, session);
                        refreshCount++;
                        continue;
                    }

                    if (resp.StatusCode == HttpStatusCode.NotFound)
                    {
                        await Task.Delay(2000, timeout.Token);
                        continue;
                    }

                    if (!resp.IsSuccessStatusCode)
                        return (false, session, refreshCount);

                    var data = await resp.Content.ReadFromJsonAsync<QueueStatusDto>(
                        cancellationToken: timeout.Token);

                    if (data?.Status == "Active")
                        return (true, session, refreshCount);

                    if (!signalRConnected)
                        await Task.Delay(data?.NextPollDelay ?? 3000, timeout.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
            finally
            {
                await hub.DisposeAsync();
            }

            return (false, session, refreshCount);
        }
    }
}
