using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PlatformA.Game.Server
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== 🔥 High Performance Game Server (Level 5) ===");

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

                // 접속된 클라이언트를 별도 Task로 처리 (Fire and Forget)
                _ = HandleClientAsync(clientSocket);
            }
        }

        static async Task HandleClientAsync(Socket client)
        {
            Console.WriteLine($"[Client] Connected: {client.RemoteEndPoint}");

            // TODO:접속자가 1만 명이면? -> 1만 개의 배열이 힙 메모리에 생성됩니다.
            // 🚨 [문제점] 여기서 매번 new byte[]를 하면 GC가 폭발합니다.
            // (나중에 ArrayPool과 Memory<T>로 최적화할 예정)
            byte[] buffer = new byte[1024];

            try
            {
                while (true)
                {
                    // 5. 데이터 수신 (ReceiveAsync)
                    // 받은 데이터 크기를 리턴함. 0이면 연결 끊김.
                    int received = await client.ReceiveAsync(buffer, SocketFlags.None);

                    if (received == 0)
                    {
                        Console.WriteLine($"[Client] Disconnected (Graceful): {client.RemoteEndPoint}");
                        break;
                    }

                    // TODO: 패킷 내용을 확인할 때마다 string 객체를 새로 만듭니다. 이것도 힙 메모리 할당입니다. 
                    string message = Encoding.UTF8.GetString(buffer, 0, received);
                    Console.WriteLine($"[Recv] {message.Trim()}");

                    // 6. 에코 (받은 거 그대로 전송)
                    await client.SendAsync(new ArraySegment<byte>(buffer, 0, received), SocketFlags.None);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] {ex.Message}");
            }
            finally
            {
                client.Shutdown(SocketShutdown.Both);
                client.Close();
            }
        }
    }
}