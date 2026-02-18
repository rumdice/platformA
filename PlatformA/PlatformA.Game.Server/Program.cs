using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Buffers;

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
            //byte[] buffer = new byte[1024];

            // 🔥 [최적화 1] ArrayPool에서 버퍼 빌리기 (Allocation-Free)
            // Shared: 전역 공유 풀
            // Rent(1024): 최소 1024 바이트 이상의 배열을 줘. (딱 1024가 아닐 수도 있음. 2048일 수도 있음)
            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024);

            try
            {
                while (true)
                {
                    // 🔥 [최적화 2] Memory<T> 사용
                    // Socket.ReceiveAsync는 이제 ArraySegment 대신 Memory<byte>를 받습니다.
                    // buffer 전체가 아니라, 실제로 빌린 만큼만 슬라이싱해서 넘겨줍니다.
                    var memory = new Memory<byte>(buffer);

                    // 5. 데이터 수신 (ReceiveAsync)
                    // 받은 데이터 크기를 리턴함. 0이면 연결 끊김.
                    //int received = await client.ReceiveAsync(buffer, SocketFlags.None);
                    int received = await client.ReceiveAsync(memory, SocketFlags.None);


                    if (received == 0)
                    {
                        Console.WriteLine($"[Client] Disconnected (Graceful): {client.RemoteEndPoint}");
                        break;
                    }

                    // TODO: 패킷 내용을 확인할 때마다 string 객체를 새로 만듭니다. 이것도 힙 메모리 할당입니다. 
                    // 받은 데이터 처리 (String 변환은 테스트용이라 new 허용. 나중엔 이것도 없앨 예정)
                    string message = Encoding.UTF8.GetString(buffer, 0, received);
                    Console.WriteLine($"[Recv] {message.Trim()}");


                    // 6. 에코 (받은 거 그대로 전송)
                    //await client.SendAsync(new ArraySegment<byte>(buffer, 0, received), SocketFlags.None);

                    // 에코 전송
                    // (보낼 때도 받은 만큼만 잘라서 보냄)
                    await client.SendAsync(new ReadOnlyMemory<byte>(buffer, 0, received), SocketFlags.None);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] {ex.Message}");
            }
            finally
            {
                // 🚨 [필수] 다 쓴 버퍼는 반드시 반납해야 함!
                // 반납 안 하면 '메모리 누수'와 똑같은 현상 발생 (풀이 고갈됨)
                ArrayPool<byte>.Shared.Return(buffer);

                client.Shutdown(SocketShutdown.Both);
                client.Close();
            }
        }
    }
}