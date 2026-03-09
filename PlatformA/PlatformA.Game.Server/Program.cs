using System.Net;
using System.Net.Sockets;
using PlatformA.Game.Server.Core;
using PlatformA.Game.Server.Packet;

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
            PlatformA.Game.Server.Packet.PacketManager.Instance.Register(); // 🚀 추가
            
            // 패킷 제너레이터 테스트
            PacketGenTest();

            // 1. 소켓 생성 (IPv4, Stream(TCP), TCP)
            using Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // 2. 포트 바인딩 (Any: 모든 IP에서 접속 허용)
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 7777);
            listener.Bind(endPoint);

            // 3. 리슨 시작 (Backlog: 대기열 100개)
            listener.Listen(100);

            Console.WriteLine($"[Server] Listening on {endPoint}...");

            while (true)
            {
                // 4. 클라이언트 접속 대기 (비동기)
                // AcceptAsync는 새로운 클라이언트와 통신할 'Socket'을 반환합니다.
                Socket clientSocket = await listener.AcceptAsync();

                // 🔥 프레임워크 사용: 소켓이 연결될 때마다 새 세션을 만들고 Start!
                Session session = new GameSession();
                session.Start(clientSocket);
            }
        }
        
    }
}