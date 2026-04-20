using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata;
using PlatformA.Game.Server.Network;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Library.Network;
using PlatformA.Library.Packets;

namespace PlatformA.Game.Server
{
    internal class Program
    {
        static void PacketGenTest()
        {
            Console.WriteLine("=== 📦 Packet Generator Unit Test ===");

            // 1. 송신측: 데이터 설정 및 직렬화
            var sendPacket = new C_MovePacket { X = 10.5f, Y = 20.7f, Z = 5.0f };

            // 스택 메모리에 바이트 배열 할당 (GC 부하 없음)
            Span<byte> buffer = stackalloc byte[C_MovePacket.Size];

            // 요정이 만든 Serialize 호출
            sendPacket.Serialize(buffer);
            Console.WriteLine($"[Step 1] 직렬화 완료 (Size: {buffer.Length} bytes)");

            // 2. 수신측: 새로운 패킷 객체 생성 및 역직렬화
            var recvPacket = new C_MovePacket();

            // 요정이 만든 Deserialize 호출
            recvPacket.Deserialize(buffer);
            Console.WriteLine("[Step 2] 역직렬화 완료");

            // 3. 결과 검증
            Console.WriteLine($"[Result] X: {recvPacket.X}, Y: {recvPacket.Y}, Z: {recvPacket.Z}");

            if (sendPacket.X == recvPacket.X && sendPacket.Y == recvPacket.Y)
                Console.WriteLine("=> ✨ 테스트 성공: 데이터가 일치합니다.");
            else
                Console.WriteLine("=> ❌ 테스트 실패: 데이터가 오염되었습니다.");

            
        }

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
            PlatformA.Game.Server.Core.GameRoomManager.Instance.CreateRoom(1);
            Console.WriteLine("[RoomManager] 기본 1번 방(Lobby) 생성 완료.");

            // 🚀 3. Redis 이벤트 구독 (이벤트 주도 아키텍처)
            // 라이브러리에서 매칭 성공 이벤트가 터지면, 서버의 GameRoomManager가 방을 만듭니다!
            PlatformA.Library.Core.RedisManager.Instance.OnMatchSuccessReceived += (matchEvent) =>
            {
                Core.GameRoomManager.Instance.CreateRoom(matchEvent.RoomId);
            };

            // 패킷 제너레이터 테스트
            PacketGenTest();

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