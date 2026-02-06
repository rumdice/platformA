using Microsoft.AspNetCore.SignalR;
using PlatformA.Matching.API.Services;

namespace PlatformA.Matching.API.Hubs
{
    public class MatchingHub : Hub
    {
        private readonly EngineService _engine;

        // 1. EngineService 주입 받기 (싱글톤이라 데이터가 살아있음)
        public MatchingHub(EngineService engine)
        {
            _engine = engine;
        }

        // 클라이언트 접속 시 로그
        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"[SignalR] 접속됨: {Context.ConnectionId}");

            // 2. 🔥 [핵심] 접속하자마자 현재 호가창 스냅샷을 가져옴
            var currentBook = _engine.GetOrderBook().GetSnapshot();

            // 3. 🔥 [핵심] 접속한 '그 사람(Caller)'에게만 즉시 전송
            // (ReceiveOrderBook 이라는 이벤트는 프론트에서 이미 처리하고 있으니 바로 화면에 뜸)
            await Clients.Caller.SendAsync("ReceiveOrderBook", currentBook);

            await base.OnConnectedAsync();
        }

        // 접속 해제 시 로그
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"[SignalR] 해제됨: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }
    }
}
