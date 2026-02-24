using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Buffers;
using System.IO.Pipelines;
using PlatformA.Game.Server.Core;

namespace PlatformA.Game.Server
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== 🔥 High Performance Game Server (Level 5) ===");

            using Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 7777);

            listener.Bind(endPoint);
            listener.Listen(100);

            Console.WriteLine($"[Server] Listening on {endPoint}...");

            while (true)
            {
                Socket clientSocket = await listener.AcceptAsync();

                // 🔥 프레임워크 사용: 소켓이 연결될 때마다 새 세션을 만들고 Start!
                Session session = new GameSession();
                session.Start(clientSocket);
            }
        }

        // 프레임 워크로 개선
        //static async Task Main(string[] args)
        //{
        //    Console.WriteLine("=== 🔥 High Performance Game Server (Level 5) ===");

        //    // 1. 소켓 생성 (IPv4, Stream(TCP), TCP)
        //    using Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        //    // 2. 포트 바인딩 (Any: 모든 IP에서 접속 허용)
        //    IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 7777);
        //    listener.Bind(endPoint);

        //    // 3. 리슨 시작 (Backlog: 대기열 100개)
        //    listener.Listen(100);

        //    Console.WriteLine($"[Server] Listening on {endPoint}...");

        //    while (true)
        //    {
        //        // 4. 클라이언트 접속 대기 (비동기)
        //        // AcceptAsync는 새로운 클라이언트와 통신할 'Socket'을 반환합니다.
        //        Socket clientSocket = await listener.AcceptAsync();

        //        // 접속된 클라이언트를 별도 Task로 처리 (Fire and Forget)
        //        _ = HandleClientAsync(clientSocket);
        //    }
        //}

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
        //static async Task HandleClientAsync(Socket client)
        //{
        //    Console.WriteLine($"[Client] Connected: {client.RemoteEndPoint}");

        //    // 🔥 [핵심] 파이프 생성
        //    // 파이프는 'Writer(소켓에서 받기)'와 'Reader(패킷 분석)'로 나뉩니다.
        //    var pipe = new Pipe();

        //    // 두 개의 태스크를 동시에 돌립니다.
        //    // 1. FillPipeAsync: 소켓 -> 파이프에 데이터 들이붓기 (Writer)
        //    // 2. ReadPipeAsync: 파이프 -> 패킷 꺼내서 처리하기 (Reader)
        //    Task writing = FillPipeAsync(client, pipe.Writer);
        //    Task reading = ReadPipeAsync(client, pipe.Reader);

        //    // 두 작업이 끝날 때까지 대기
        //    await Task.WhenAll(reading, writing);

        //    Console.WriteLine($"[Client] Disconnected: {client.RemoteEndPoint}");

        //}


        // 📥 [Writer] 소켓에서 데이터를 읽어서 파이프에 씀
        //static async Task FillPipeAsync(Socket client, PipeWriter writer)
        //{
        //    const int minimumBufferSize = 512;

        //    while (true)
        //    {
        //        // 파이프에서 "쓸 수 있는 메모리"를 조금 빌려옴 (ArrayPool을 내부적으로 씀)
        //        Memory<byte> memory = writer.GetMemory(minimumBufferSize);

        //        try
        //        {
        //            // 소켓으로부터 데이터를 받아서 바로 파이프 메모리에 꽂음 (Copy 발생 X)
        //            int bytesRead = await client.ReceiveAsync(memory, SocketFlags.None);

        //            if (bytesRead == 0)
        //            {
        //                break; // 연결 끊김
        //            }

        //            // 파이프에게 "나 이만큼 썼어"라고 알려줌
        //            writer.Advance(bytesRead);
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine($"[Receive Error] {ex.Message}");
        //            break;
        //        }

        //        // Reader에게 "데이터 들어왔으니 일해!"라고 깨움
        //        FlushResult result = await writer.FlushAsync();

        //        if (result.IsCompleted)
        //        {
        //            break;
        //        }

        //    }

        //    // 다 끝났으면 파이프 닫음
        //    await writer.CompleteAsync();
        //}

        // 📤 [Reader] 파이프에서 데이터를 읽어서 패킷으로 조립 (Parsing)
        //static async Task ReadPipeAsync(Socket client, PipeReader reader)
        //{
        //    while (true)
        //    {
        //        // 파이프에 들어온 데이터를 읽음
        //        ReadResult result = await reader.ReadAsync();
        //        ReadOnlySequence<byte> buffer = result.Buffer;

        //        // 🔥 여기가 핵심! 패킷 자르기 로직
        //        // 데이터가 조금 쪼개져서 오거나 뭉쳐서 와도 여기서 처리됨
        //        while (TryReadPacket(ref buffer, out ReadOnlySequence<byte> packet))
        //        {
        //            // 완성된 패킷(packet) 처리
        //            await ProcessPacketAsync(client, packet);
        //        }

        //        // 다 처리하고 남은 데이터(자투리)가 있으면 다음으로 넘김
        //        reader.AdvanceTo(buffer.Start, buffer.End);

        //        if (result.IsCompleted)
        //        {
        //            break;
        //        }
        //    }

        //    await reader.CompleteAsync();
        //}


        // 🔍 [Parser] 패킷 헤더(2바이트)를 확인하고 자르는 함수
        //static bool TryReadPacket(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> packet)
        //{
        //    // 1. 헤더(2바이트)보다 적게 왔으면 대기
        //    if (buffer.Length < 2)
        //    {
        //        packet = default;
        //        return false;
        //    }

        //    // 2. 앞의 2바이트(패킷 길이)를 읽음
        //    // (BitConverter를 쓰기 위해 앞부분만 살짝 가져옴)
        //    var lengthBuffer = buffer.Slice(0, 2);
        //    // BigEndian인지 LittleEndian인지 주의해야 하지만 일단 심플하게 (ushort)
        //    // 여기서는 간단히 BitConverter를 쓰기 위해 배열로 복사하지만, 실제론 BinaryPrimitives를 씀
        //    byte[] lenBytes = lengthBuffer.ToArray();
        //    ushort packetLength = BitConverter.ToUInt16(lenBytes, 0);

        //    // 3. 아직 데이터가 "헤더(2) + 본문(Length)" 만큼 안 왔으면 대기
        //    if (buffer.Length < 2 + packetLength)
        //    {
        //        packet = default;
        //        return false;
        //    }

        //    // 4. 패킷 하나 완성! 잘라서 리턴
        //    packet = buffer.Slice(2, packetLength); // 헤더 제외한 본문만

        //    // 5. 원본 버퍼에서는 이미 읽은 만큼(헤더+본문) 잘라내고 포인터 이동
        //    buffer = buffer.Slice(2 + packetLength);

        //    return true;
        //}

        //// ⚙️ [Handler] 실제 비즈니스 로직
        //static async Task ProcessPacketAsync(Socket client, ReadOnlySequence<byte> packet)
        //{
        //    // ReadOnlySequence는 메모리가 여러 조각으로 나뉘어 있을 수 있어서, 
        //    // 문자열 변환을 위해선 배열로 합쳐야 할 수도 있음. (여기선 편의상 ToArray 사용)
        //    // -> 성능을 위해선 Encoding.GetString(packet) 확장 메서드를 만드는 게 좋음
        //    string msg = Encoding.UTF8.GetString(packet.ToArray());
        //    Console.WriteLine($"[Packet Received] {msg}");

        //    // 에코 전송 (헤더 다시 붙여서 보내야 함은 생략. 그냥 원본 보냄 테스트용)
        //    // 실제로는 Send도 패킷 구조 맞춰야 함
        //}
    }


}