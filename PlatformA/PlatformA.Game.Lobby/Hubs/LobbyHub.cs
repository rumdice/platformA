using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PlatformA.Game.Lobby.Services;

namespace PlatformA.Game.Lobby.Hubs
{
    /// <summary>
    /// Game.Lobby SignalR Hub.
    /// 클라이언트는 JWT를 QueryString(?access_token=) 또는 Authorization 헤더로 전달합니다.
    /// </summary>
    [Authorize]
    public class LobbyHub : Hub
    {
        private readonly LobbyPresenceService _presence;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<LobbyHub> _logger;

        public LobbyHub(
            LobbyPresenceService presence,
            IHttpClientFactory httpClientFactory,
            ILogger<LobbyHub> logger)
        {
            _presence = presence;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            string? idStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idStr, out int userId) || userId <= 0)
            {
                _logger.LogWarning("[Lobby] 인증 실패 — 연결 거부 connectionId={CId}", Context.ConnectionId);
                Context.Abort();
                return;
            }

            _presence.Register(userId, Context.ConnectionId);
            Context.Items["UserId"] = userId;

            _logger.LogInformation("[Lobby] 연결 — User_{UserId} connectionId={CId}", userId, Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (Context.Items.TryGetValue("UserId", out object? val) && val is int userId)
            {
                _presence.Unregister(userId);
                _logger.LogInformation("[Lobby] 연결 해제 — User_{UserId}", userId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>매칭 신청. Matching.API를 호출하여 즉시 매칭하거나 대기열에 진입합니다.</summary>
        public async Task RequestMatch(string gameType)
        {
            if (!TryGetUserId(out int userId))
                return;

            _logger.LogInformation("[Lobby] 매칭 신청 — User_{UserId} gameType={GameType}", userId, gameType);

            try
            {
                using HttpClient http = _httpClientFactory.CreateClient("MatchingAPI");
                string body = JsonSerializer.Serialize(new { UserId = userId, GameType = gameType });
                using HttpContent content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

                HttpResponseMessage resp = await http.PostAsync("/api/gamematch/request", content);

                if (resp.StatusCode == System.Net.HttpStatusCode.Accepted)
                {
                    // 대기열 진입 — Redis publish로 이후 MatchFound 수신 예정
                    await Clients.Caller.SendAsync("MatchQueued", new { gameType, message = "매칭 대기 중입니다." });
                    return;
                }

                if (resp.IsSuccessStatusCode)
                {
                    // 즉시 매칭 성사 — HTTP 응답으로 정보 수신 (+ Redis publish도 수신되지만 중복 처리 무시)
                    string json = await resp.Content.ReadAsStringAsync();
                    using JsonDocument doc = JsonDocument.Parse(json);
                    await Clients.Caller.SendAsync("MatchFound", new
                    {
                        host = doc.RootElement.GetProperty("host").GetString(),
                        port = doc.RootElement.GetProperty("port").GetInt32(),
                        roomId = doc.RootElement.GetProperty("roomId").GetString(),
                        gameType,
                    });
                    return;
                }

                _logger.LogWarning("[Lobby] Matching.API 오류 {Status} — User_{UserId}", resp.StatusCode, userId);
                await Clients.Caller.SendAsync("MatchError", new { message = "매칭 요청 처리 중 오류가 발생했습니다." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Lobby] RequestMatch 오류 — User_{UserId}", userId);
                await Clients.Caller.SendAsync("MatchError", new { message = "매칭 서버에 연결할 수 없습니다." });
            }
        }

        /// <summary>매칭 취소. 대기열에서 제거합니다.</summary>
        public async Task CancelMatch(string gameType = "")
        {
            if (!TryGetUserId(out int userId))
                return;

            _logger.LogInformation("[Lobby] 매칭 취소 — User_{UserId}", userId);

            try
            {
                using HttpClient http = _httpClientFactory.CreateClient("MatchingAPI");

                // JWT 전달 (Matching.API는 수동 토큰 검증)
                string? token = Context.GetHttpContext()?.Request.Query["access_token"].ToString();
                if (!string.IsNullOrEmpty(token))
                    http.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                HttpResponseMessage resp = await http.DeleteAsync("/api/gamematch/CancelMatch");

                if (resp.IsSuccessStatusCode)
                    await Clients.Caller.SendAsync("MatchCancelled", new { message = "매칭이 취소되었습니다." });
                else if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    await Clients.Caller.SendAsync("MatchCancelled", new { message = "대기열에 없습니다." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Lobby] CancelMatch 오류 — User_{UserId}", userId);
            }
        }

        /// <summary>대기열 상태 조회.</summary>
        public async Task GetMatchStatus()
        {
            if (!TryGetUserId(out int userId))
                return;

            try
            {
                using HttpClient http = _httpClientFactory.CreateClient("MatchingAPI");
                string? token = Context.GetHttpContext()?.Request.Query["access_token"].ToString();
                if (!string.IsNullOrEmpty(token))
                    http.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                HttpResponseMessage resp = await http.GetAsync("/api/gamematch/Status");
                if (resp.IsSuccessStatusCode)
                {
                    string json = await resp.Content.ReadAsStringAsync();
                    await Clients.Caller.SendAsync("MatchStatus", json);
                }
                else
                {
                    await Clients.Caller.SendAsync("MatchStatus", (object?)null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Lobby] GetMatchStatus 오류 — User_{UserId}", userId);
            }
        }

        private bool TryGetUserId(out int userId)
        {
            if (Context.Items.TryGetValue("UserId", out object? val) && val is int id)
            {
                userId = id;
                return true;
            }

            userId = 0;
            _logger.LogWarning("[Lobby] 인증되지 않은 Hub 메서드 호출 — connectionId={CId}", Context.ConnectionId);
            return false;
        }
    }
}
