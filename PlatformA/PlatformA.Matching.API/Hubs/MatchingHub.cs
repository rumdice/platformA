using Microsoft.AspNetCore.SignalR;
using PlatformA.Library.Common;

namespace PlatformA.Matching.API.Hubs
{
    /// <summary>
    /// 게임서버 매칭용 SignalR 허브.
    /// JWT 검증 후 User_{playerId} 그룹에 등록하고 MatchFound / MatchTimeout 이벤트를 수신합니다.
    /// </summary>
    public class MatchingHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            string? jwtToken = null;

            if (httpContext != null)
            {
                string authHeader = httpContext.Request.Headers["Authorization"].ToString();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer"))
                    jwtToken = authHeader.Substring(7);

                if (string.IsNullOrEmpty(jwtToken))
                    jwtToken = httpContext.Request.Query["access_token"].ToString();
            }

            if (!string.IsNullOrEmpty(jwtToken))
            {
                int playerId = TokenManager.ValidateTokenAndGetUserId(jwtToken);

                if (playerId > 0)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{playerId}");
                    Context.Items["PlayerId"] = playerId;
                    Console.WriteLine($"[SignalR] 유저 {playerId} 접속 및 그룹 등록 완료");
                }
                else
                {
                    Console.WriteLine($"[SignalR] 접속 거부: 유효하지 않은 토큰입니다.");
                    Context.Abort();
                }
            }
            else
            {
                Console.WriteLine($"[SignalR] 접속 거부: 토큰이 전달되지 않았습니다.");
                Context.Abort();
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"[SignalR] 해제됨: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }
    }
}
