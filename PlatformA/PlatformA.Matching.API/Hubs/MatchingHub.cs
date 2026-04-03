using Microsoft.AspNetCore.SignalR;
using PlatformA.Library.Common;
using PlatformA.Matching.API.Services;

namespace PlatformA.Matching.API.Hubs
{
    /// <summary>
    /// 주식 매도 매칭 엔진과 게임서버 매칭을 혼용하다가 지금은 게임서버 매칭 용도로만 사용하는 SignalR 허브 클래스.
    /// </summary>
    public class MatchingHub : Hub
    {
        private readonly EngineService _engine; // 주식 매도 메칭 안쓰임.
        private readonly GameMatchService _gamemMatchService;

        public MatchingHub(EngineService engine, GameMatchService gamemMatchService)
        {
            _engine = engine;
            _gamemMatchService = gamemMatchService;
        }


        /// <summary>
        /// 유저 접속시
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            string? jwtToken = null;

            if (httpContext != null)
            {
                // 먼저 HTTP 헤더에서 토큰을 찾기. (C# 더미 클라이언트용)
                string authHeader = httpContext.Request.Headers["Authorization"].ToString();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer"))
                {
                    jwtToken = authHeader.Substring(7); // "Bearer " 이후의 순수 토큰만 추출
                }

                // 헤더에 없다면 쿼리스트링에서 찾아봅니다. (웹 브라우저 클라이언트용)
                if (string.IsNullOrEmpty(jwtToken))
                {
                    jwtToken = httpContext.Request.Query["access_token"].ToString();
                }
            }

            // 토큰 검증 진행
            if (!string.IsNullOrEmpty(jwtToken))
            {
                int playerId = TokenManager.ValidateTokenAndGetUserId(jwtToken);

                if (playerId > 0)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{playerId}"); // 커넥션ID를 User_1 라는 그룹으로 묶는다.
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


        /// <summary>
        /// 유저 접속 해제시
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"[SignalR] 해제됨: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }


        
    }
}
