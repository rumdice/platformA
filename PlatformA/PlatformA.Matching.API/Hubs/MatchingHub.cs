using Microsoft.AspNetCore.SignalR;
using PlatformA.Library.Common;
using PlatformA.Matching.API.Services;

namespace PlatformA.Matching.API.Hubs
{
    public class MatchingHub : Hub
    {
        private readonly EngineService _engine;
        private readonly GameMatchService _gamemMatchService;

        // 1. EngineService 주입 받기 (싱글톤이라 데이터가 살아있음)
        public MatchingHub(EngineService engine, GameMatchService gamemMatchService)
        {
            _engine = engine;
            _gamemMatchService = gamemMatchService;
        }


        // 클라이언트 접속 시 로그

        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            string jwtToken = null;

            if (httpContext != null)
            {
                // 🚀 1. 먼저 HTTP 헤더에서 토큰을 찾아봅니다. (C# 더미 클라이언트용)
                string authHeader = httpContext.Request.Headers["Authorization"].ToString();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    jwtToken = authHeader.Substring(7); // "Bearer " 이후의 순수 토큰만 추출
                }

                // 🚀 2. 헤더에 없다면 쿼리스트링에서 찾아봅니다. (웹 브라우저 클라이언트용)
                if (string.IsNullOrEmpty(jwtToken))
                {
                    jwtToken = httpContext.Request.Query["access_token"];
                }
            }

            // 🚀 3. 토큰 검증 진행
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


        // 접속 해제 시 로그
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"[SignalR] 해제됨: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }

        public async Task RequestMatch()
        {
            if (Context.Items.TryGetValue("PlayerId", out var playerIdObj) && playerIdObj is int playerId)
            {
                Console.WriteLine($"[매칭] 유저 {playerId} 가 매칭 큐에 진입했습니다.");
                // _engine.AddPlayerToQueue(playerId, Context.ConnectionId);
            }
            else
            {
                Console.WriteLine("[매칭] 인증되지 않은 유저의 매칭 요청입니다.");
            }
        }



    }
}
