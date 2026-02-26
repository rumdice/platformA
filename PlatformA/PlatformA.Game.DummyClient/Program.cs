// See https://aka.ms/new-console-template for more information
using System.Net.Sockets;
using System.Text;

namespace PlatformA.Game.DummyClient
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== 👾 Dummy Client for Pipeline Server ===");
            await Task.Delay(1000); // 서버가 켜질 시간 확보

            using Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                // 포트번호 7777로 접속
                await client.ConnectAsync("127.0.0.1", 7777);
                Console.WriteLine("서버 접속 성공!\n");

                // 📡 [추가됨] 서버로부터 데이터를 계속 수신하는 백그라운드 작업 시작 (Fire and Forget)
                _ = ReceiveLoopAsync(client);

                //// -------------------------------------------------
                //// 1. 정상 패킷 전송 테스트
                //// -------------------------------------------------
                //Console.WriteLine("--- 1. 정상 패킷 전송 ---");
                //await SendPacketAsync(client, "Hello Pipeline!");
                //await Task.Delay(1000);

                //// -------------------------------------------------
                //// 2. 패킷 뭉침 (Sticking) 테스트
                //// -------------------------------------------------
                //// 3개의 패킷을 하나의 배열로 뭉쳐서 한 번의 Send로 전송
                //Console.WriteLine("\n--- 2. 패킷 뭉침 (Sticking) 테스트 ---");
                //byte[] packet1 = MakePacket("First Packet");
                //byte[] packet2 = MakePacket("Second Packet");
                //byte[] packet3 = MakePacket("Third Packet");

                //byte[] combined = new byte[packet1.Length + packet2.Length + packet3.Length];
                //Buffer.BlockCopy(packet1, 0, combined, 0, packet1.Length);
                //Buffer.BlockCopy(packet2, 0, combined, packet1.Length, packet2.Length);
                //Buffer.BlockCopy(packet3, 0, combined, packet1.Length + packet2.Length, packet3.Length);

                //await client.SendAsync(combined, SocketFlags.None);
                //Console.WriteLine("[Send] 3개의 패킷을 뭉쳐서 한번에 전송함!");
                //await Task.Delay(1000);

                //// -------------------------------------------------
                //// 3. 패킷 찢어짐 (Splitting) 테스트
                //// -------------------------------------------------
                //Console.WriteLine("\n--- 3. 패킷 찢어짐 (Splitting) 테스트 ---");
                //byte[] splitPacket = MakePacket("This is a very long split packet test.");

                //int half = splitPacket.Length / 2;

                //// 3-1. 절반만 먼저 보냄
                //await client.SendAsync(splitPacket.AsMemory(0, half), SocketFlags.None);
                //Console.WriteLine($"[Send] 절반({half} bytes) 전송 완료. 2초 대기...");

                //// 3-2. 2초 대기 (이 동안 서버는 패킷이 완성되길 기다려야 함)
                //await Task.Delay(2000);

                //// 3-3. 나머지 절반 전송
                //await client.SendAsync(splitPacket.AsMemory(half), SocketFlags.None);
                //Console.WriteLine("[Send] 나머지 절반 전송 완료!");


                //// 4. 문자열 전송을 버리고 바이너리 패킷 전송 테스트
                //Console.WriteLine("--- 바이너리 패킷 전송 테스트 ---");
                //await SendMovePacketAsync(client, 10.5f, 20.0f, 1.2f);
                //await Task.Delay(100);
                //await SendMovePacketAsync(client, -5.0f, 15.5f, 0.0f);


                // 5. 브로드케스팅 패킷 송/수신 목적의 테스트
                // --- 테스트 시나리오 ---
                Console.WriteLine("엔터를 누를 때마다 이동 패킷(C_Move)을 서버로 전송합니다.");
                Console.WriteLine("종료하려면 'q'를 입력하세요.\n");

                Random rand = new Random();

                while (true)
                {
                    string input = Console.ReadLine();
                    if (input?.ToLower() == "q") break;

                    // 랜덤한 좌표로 이동 패킷 전송
                    float x = rand.Next(-50, 50);
                    float y = rand.Next(-50, 50);
                    float z = 0f;

                    await SendMovePacketAsync(client, x, y, z);
                }



                //// 모든 테스트 종료
                Console.WriteLine("\n모든 테스트 완료. 종료하려면 엔터를 누르세요.");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"접속 실패: {ex.Message}");
            }
        }

        // -------------------------------------------------
        // 헬퍼 메서드: [Length(2 byte)] + [Message] 구조의 패킷 만들기
        // -------------------------------------------------
        static byte[] MakePacket(string message)
        {
            byte[] body = Encoding.UTF8.GetBytes(message);
            ushort len = (ushort)body.Length; // 본문 길이

            byte[] packet = new byte[2 + body.Length];

            // 길이 정보(2바이트)를 배열의 맨 앞에 넣음
            byte[] header = BitConverter.GetBytes(len);
            Array.Copy(header, 0, packet, 0, 2);

            // 본문 정보를 헤더 뒤에 이어 붙임
            Array.Copy(body, 0, packet, 2, body.Length);

            return packet;
        }

        static async Task SendPacketAsync(Socket client, string message)
        {
            byte[] packet = MakePacket(message);
            await client.SendAsync(packet, SocketFlags.None);
            Console.WriteLine($"[Send] {message}");
        }

        // 1. [동기 함수] Span을 이용한 패킷 조립 전용 메서드 (await 없음 -> 에러 안 남!)
        static byte[] MakeMovePacket(float x, float y, float z)
        {
            ushort packetSize = 16;
            ushort packetId = 1; // C_Move

            byte[] buffer = new byte[packetSize];
            Span<byte> span = buffer.AsSpan();

            // 1. 헤더 조립 (사이즈 2 + ID 2)
            BitConverter.TryWriteBytes(span.Slice(0, 2), packetSize);
            BitConverter.TryWriteBytes(span.Slice(2, 2), packetId);

            // 2. 본문(Payload) 조립 (X, Y, Z 각각 4바이트)
            BitConverter.TryWriteBytes(span.Slice(4, 4), x);
            BitConverter.TryWriteBytes(span.Slice(8, 4), y);
            BitConverter.TryWriteBytes(span.Slice(12, 4), z);

            return buffer;
        }


        // 2. [비동기 함수] 완성된 패킷을 전송만 하는 메서드
        static async Task SendMovePacketAsync(Socket client, float x, float y, float z)
        {
            // 조립은 다른 함수에 맡기고 결과물(byte[])만 받아옴
            byte[] packet = MakeMovePacket(x, y, z);

            // 여기엔 Span이 없으므로 마음껏 await 가능!
            await client.SendAsync(packet, SocketFlags.None);
            Console.WriteLine($"[Send] C_Move ({x}, {y}, {z}) - 16 bytes");
        }

        // 🎧 [추가됨] 서버가 보내는 패킷을 계속 듣는 루프
        static async Task ReceiveLoopAsync(Socket client)
        {
            byte[] buffer = new byte[1024];

            try
            {
                while (true)
                {
                    int received = await client.ReceiveAsync(buffer, SocketFlags.None);
                    if (received == 0)
                    {
                        Console.WriteLine("서버와 연결이 끊어졌습니다.");
                        break;
                    }

                    // 1. 헤더 파싱 (사이즈 2, ID 2)
                    // (주의: 완벽하게 하려면 클라이언트도 Pipeline을 써야 하지만, 더미 테스트용이므로 단순화함)
                    if (received >= 4)
                    {
                        ushort size = BitConverter.ToUInt16(buffer, 0);
                        ushort packetId = BitConverter.ToUInt16(buffer, 2);

                        // 2. 패킷 ID가 2 (S_Move) 인지 확인
                        if (packetId == 2) // PacketID.S_Move
                        {
                            // 3. 본문 파싱 (PlayerId, X, Y, Z)
                            int playerId = BitConverter.ToInt32(buffer, 4);
                            float x = BitConverter.ToSingle(buffer, 8);
                            float y = BitConverter.ToSingle(buffer, 12);
                            float z = BitConverter.ToSingle(buffer, 16);

                            Console.WriteLine($"\n  [Broadcast 📡] 플레이어 {playerId} 이동 -> X:{x}, Y:{y}, Z:{z}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"수신 에러: {ex.Message}");
            }
        }

    }
}