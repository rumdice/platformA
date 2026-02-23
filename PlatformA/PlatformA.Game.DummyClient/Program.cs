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

                // -------------------------------------------------
                // 1. 정상 패킷 전송 테스트
                // -------------------------------------------------
                Console.WriteLine("--- 1. 정상 패킷 전송 ---");
                await SendPacketAsync(client, "Hello Pipeline!");
                await Task.Delay(1000);

                // -------------------------------------------------
                // 2. 패킷 뭉침 (Sticking) 테스트
                // -------------------------------------------------
                // 3개의 패킷을 하나의 배열로 뭉쳐서 한 번의 Send로 전송
                Console.WriteLine("\n--- 2. 패킷 뭉침 (Sticking) 테스트 ---");
                byte[] packet1 = MakePacket("First Packet");
                byte[] packet2 = MakePacket("Second Packet");
                byte[] packet3 = MakePacket("Third Packet");

                byte[] combined = new byte[packet1.Length + packet2.Length + packet3.Length];
                Buffer.BlockCopy(packet1, 0, combined, 0, packet1.Length);
                Buffer.BlockCopy(packet2, 0, combined, packet1.Length, packet2.Length);
                Buffer.BlockCopy(packet3, 0, combined, packet1.Length + packet2.Length, packet3.Length);

                await client.SendAsync(combined, SocketFlags.None);
                Console.WriteLine("[Send] 3개의 패킷을 뭉쳐서 한번에 전송함!");
                await Task.Delay(1000);

                // -------------------------------------------------
                // 3. 패킷 찢어짐 (Splitting) 테스트
                // -------------------------------------------------
                Console.WriteLine("\n--- 3. 패킷 찢어짐 (Splitting) 테스트 ---");
                byte[] splitPacket = MakePacket("This is a very long split packet test.");

                int half = splitPacket.Length / 2;

                // 3-1. 절반만 먼저 보냄
                await client.SendAsync(splitPacket.AsMemory(0, half), SocketFlags.None);
                Console.WriteLine($"[Send] 절반({half} bytes) 전송 완료. 2초 대기...");

                // 3-2. 2초 대기 (이 동안 서버는 패킷이 완성되길 기다려야 함)
                await Task.Delay(2000);

                // 3-3. 나머지 절반 전송
                await client.SendAsync(splitPacket.AsMemory(half), SocketFlags.None);
                Console.WriteLine("[Send] 나머지 절반 전송 완료!");

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
    }
}