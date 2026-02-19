using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Buffers;
using System.IO.Pipelines;

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

        //static async Task HandleClientAsync(Socket client)
        //{
        //    Console.WriteLine($"[Client] Connected: {client.RemoteEndPoint}");

        //    // TODO:접속자가 1만 명이면? -> 1만 개의 배열이 힙 메모리에 생성됩니다.
        //    // 🚨 [문제점] 여기서 매번 new byte[]를 하면 GC가 폭발합니다.
        //    // (나중에 ArrayPool과 Memory<T>로 최적화할 예정)
        //    //byte[] buffer = new byte[1024];

        //    // 🔥 [최적화 1] ArrayPool에서 버퍼 빌리기 (Allocation-Free)
        //    // Shared: 전역 공유 풀
        //    // Rent(1024): 최소 1024 바이트 이상의 배열을 줘. (딱 1024가 아닐 수도 있음. 2048일 수도 있음)
        //    byte[] buffer = ArrayPool<byte>.Shared.Rent(1024);

        //    try
        //    {
        //        while (true)
        //        {
        //            // 🔥 [최적화 2] Memory<T> 사용
        //            // Socket.ReceiveAsync는 이제 ArraySegment 대신 Memory<byte>를 받습니다.
        //            // buffer 전체가 아니라, 실제로 빌린 만큼만 슬라이싱해서 넘겨줍니다.
        //            var memory = new Memory<byte>(buffer);

        //            // 5. 데이터 수신 (ReceiveAsync)
        //            // 받은 데이터 크기를 리턴함. 0이면 연결 끊김.
        //            //int received = await client.ReceiveAsync(buffer, SocketFlags.None);
        //            int received = await client.ReceiveAsync(memory, SocketFlags.None);


        //            if (received == 0)
        //            {
        //                Console.WriteLine($"[Client] Disconnected (Graceful): {client.RemoteEndPoint}");
        //                break;
        //            }

        //            // TODO: 패킷 내용을 확인할 때마다 string 객체를 새로 만듭니다. 이것도 힙 메모리 할당입니다. 
        //            // 받은 데이터 처리 (String 변환은 테스트용이라 new 허용. 나중엔 이것도 없앨 예정)
        //            string message = Encoding.UTF8.GetString(buffer, 0, received);
        //            Console.WriteLine($"[Recv] {message.Trim()}");


        //            // 6. 에코 (받은 거 그대로 전송)
        //            //await client.SendAsync(new ArraySegment<byte>(buffer, 0, received), SocketFlags.None);

        //            // 에코 전송
        //            // (보낼 때도 받은 만큼만 잘라서 보냄)
        //            await client.SendAsync(new ReadOnlyMemory<byte>(buffer, 0, received), SocketFlags.None);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"[Error] {ex.Message}");
        //    }
        //    finally
        //    {
        //        // 🚨 [필수] 다 쓴 버퍼는 반드시 반납해야 함!
        //        // 반납 안 하면 '메모리 누수'와 똑같은 현상 발생 (풀이 고갈됨)
        //        ArrayPool<byte>.Shared.Return(buffer);

        //        client.Shutdown(SocketShutdown.Both);
        //        client.Close();
        //    }
        //}


        /// <summary>
        /// TCP 스트림을 개선한 PipeLine 도입 :  TODO: 패킷 버퍼링을 직접 구현하는건 미련한 짓일까?
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        static async Task HandleClientAsync(Socket client)
        {
            Console.WriteLine($"[Client] Connected: {client.RemoteEndPoint}");

            // 🔥 [핵심] 파이프 생성
            // 파이프는 'Writer(소켓에서 받기)'와 'Reader(패킷 분석)'로 나뉩니다.
            var pipe = new Pipe();

            // 두 개의 태스크를 동시에 돌립니다.
            // 1. FillPipeAsync: 소켓 -> 파이프에 데이터 들이붓기 (Writer)
            // 2. ReadPipeAsync: 파이프 -> 패킷 꺼내서 처리하기 (Reader)
            Task writing = FillPipeAsync(client, pipe.Writer);
            Task reading = ReadPipeAsync(client, pipe.Reader);

            // 두 작업이 끝날 때까지 대기
            await Task.WhenAll(reading, writing);

            Console.WriteLine($"[Client] Disconnected: {client.RemoteEndPoint}");

        }


        // 📥 [Writer] 소켓에서 데이터를 읽어서 파이프에 씀
        static async Task FillPipeAsync(Socket client, PipeWriter writer)
        {
            const int minimumBufferSize = 512;

            while (true)
            {
                // 파이프에서 "쓸 수 있는 메모리"를 조금 빌려옴 (ArrayPool을 내부적으로 씀)
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);

                try
                {
                    // 소켓으로부터 데이터를 받아서 바로 파이프 메모리에 꽂음 (Copy 발생 X)
                    int bytesRead = await client.ReceiveAsync(memory, SocketFlags.None);

                    if (bytesRead == 0)
                    {
                        break; // 연결 끊김
                    }

                    // 파이프에게 "나 이만큼 썼어"라고 알려줌
                    writer.Advance(bytesRead);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Receive Error] {ex.Message}");
                    break;
                }

                // Reader에게 "데이터 들어왔으니 일해!"라고 깨움
                FlushResult result = await writer.FlushAsync();

                if (result.IsCompleted)
                {
                    break;
                }

            }

            // 다 끝났으면 파이프 닫음
            await writer.CompleteAsync();
        }

        // 📤 [Reader] 파이프에서 데이터를 읽어서 패킷으로 조립 (Parsing)
        static async Task ReadPipeAsync(Socket client, PipeReader reader)
        {
        }
    }
}