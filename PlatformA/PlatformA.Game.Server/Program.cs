using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using PlatformA.Game.Server.Network;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Library.Network;
using PlatformA.Library.Packets;

namespace PlatformA.Game.Server
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== 🔥 High Performance Game Server (Level 6 - step 1) ===");

            // 패킷매니저 초기화
            PacketManager<GameSession>.Instance.Register(); // 🚀 추가

            // Redis 초기화 (콘솔 로거를 통해 Polly 이벤트가 시작부터 기록됨)
            using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
            RedisManager.Instance.Init(
                Consts.REDIS_CONNECTION_STRING,
                loggerFactory.CreateLogger<RedisManager>());

            // 🚀 서버 시작 시 기본 1번 방 생성
            PlatformA.Library.Game.Core.GameRoomManager.Instance.CreateRoom(1);
            Console.WriteLine("[RoomManager] 기본 1번 방(Lobby) 생성 완료.");

            // 🚀 3. Redis 이벤트 구독 (이벤트 주도 아키텍처)
            // 라이브러리에서 매칭 성공 이벤트가 터지면, 서버의 GameRoomManager가 방을 만듭니다!
            PlatformA.Library.Core.RedisManager.Instance.OnMatchSuccessReceived += (matchEvent) =>
            {
                PlatformA.Library.Game.Core.GameRoomManager.Instance.CreateRoom(matchEvent.RoomId);
            };

            // 1. 소켓 생성 (IPv4, Stream(TCP), TCP)
            using Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // 2. 포트 바인딩 (Any: 모든 IP에서 접속 허용)
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 7777);
            listener.Bind(endPoint);

            // 3. 리슨 시작 (Backlog: 대기열 1000개)
            listener.Listen(1000);

            Console.WriteLine($"[Server] Listening on {endPoint}...");

            while (true)
            {
                try
                {
                    // 4. 클라이언트 접속 대기 (비동기)
                    // AcceptAsync는 새로운 클라이언트와 통신할 'Socket'을 반환합니다.
                    Socket clientSocket = await listener.AcceptAsync();

                    // 🔥 프레임워크 사용: 소켓이 연결될 때마다 새 세션을 만들고 Start!
                    Session session = new GameSession();
                    session.Start(clientSocket);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Server Error] Accept 중 예외 발생 (무시하고 계속 진행): {ex.Message}");
                    // 잠시 대기하여 CPU 폭주(무한 루프 에러) 방지
                    await Task.Delay(100);
                }
            }
        }

    }
}
