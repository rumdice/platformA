using PlatformA.Library.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PlatformA.Game.DummyClient.Scenarios
{

    public class GameClientScenario
    {
        // 🚀 방금 띄운 Auth.API의 실제 주소 (포트 번호를 Swagger 창에 뜬 번호로 꼭 바꿔주세요!)
        private const string AUTH_API_URL = "https://localhost:7088/api/Auth/login";


        public static async Task RunAsync()
        {
            Console.Clear();
            Console.WriteLine("=== 👾 게임 서버 인터랙티브 클라이언트 ===");

            // 기존 DummyClient 안의 Main 함수에 있던 
            // Auth.API 로그인 -> 소켓 연결 -> C_Move 발송 루프 로직을 여기에 넣습니다.

            Console.WriteLine("=== 👾 Dummy Client for Pipeline Server ===");
            await Task.Delay(1000); // 서버가 켜질 시간 확보

            // =================================================================
            // 🌐 [STEP 1] 웹 서버(Auth.API)에 HTTP POST로 로그인 요청하기
            // =================================================================
            Console.WriteLine("[Web] Auth.API 에 로그인을 시도합니다...");

            var userName = GenerateTestUserName();

            string realToken = await LoginToAuthServerAsync(userName, "1234");
            //string realToken = await LoginToAuthServerAsync("test", "1234");

            if (string.IsNullOrEmpty(realToken))
            {
                Console.WriteLine("[Web] 로그인 실패! 프로그램을 종료합니다.");
                return;
            }

            // =================================================================
            // 🎮 [STEP 2] 게임 서버(Game Server)에 TCP 소켓으로 접속하기
            // =================================================================
            using Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                // 포트번호 7777로 접속
                await client.ConnectAsync("127.0.0.1", 7777);
                Console.WriteLine("서버 접속 성공!\n");

                // 서버로부터 데이터를 계속 수신하는 백그라운드 작업 시작 (Fire and Forget)
                _ = ReceiveLoopAsync(client);

                // --- 테스트 시나리오 ---

                // 🚀 1. 로그인 (임시 유저 ID와 토큰 발급 후 전송)
                //int myPlayerId = new Random().Next(1000, 10000);
                //string myToken = GenerateTestToken(myPlayerId);

                //Console.WriteLine($"[Client] 로그인 시도 (PlayerID: {myPlayerId})");
                //Console.WriteLine($"[Client] 생성된 토큰: {myToken.Substring(0, 20)}...");

                //await SendLoginPacketAsync(client, myToken);

                //// 로그인 패킷 전송 메서드 호출 (비동기)
                await SendLoginPacketAsync(client, realToken);

                // 서버가 인증을 처리하고 방에 넣어줄 시간을 잠깐 대기
                await Task.Delay(500);



                // 2. 이동
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

                    // 이동 패킷 전송 메서드 호출 (비동기)
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


            Console.WriteLine("연결이 종료되었습니다. 엔터를 누르면 메뉴로 돌아갑니다.");
            Console.ReadLine();
        }

        // MakeLoginPacket 등 기존 패킷 조립 함수들도 이 클래스 밑에 둡니다.
        // 1. [동기 함수] Span을 이용한 패킷 조립 전용 메서드 (await 없음 -> 에러 안 남!)
        static byte[] MakeMovePacket(float x, float y, float z)
        {
            // C_Move 패킷은 헤더(4) + X,Y,Z(12) = 16바이트로 크기 고정!
            ushort packetSize = 16;
            ushort packetId = (ushort)PacketID.C_Move;

            C_MovePacket movePacket = new C_MovePacket()
            {
                X = x,
                Y = y,
                Z = z
            };

            byte[] sendBuffer = new byte[packetSize];
            Span<byte> span = sendBuffer.AsSpan();

            // 🚀 1. 헤더 조립 (0~3번지) : 서버 소켓 엔진이 읽을 수 있도록 직접 기록
            BitConverter.TryWriteBytes(span.Slice(0, 2), packetSize);
            BitConverter.TryWriteBytes(span.Slice(2, 2), packetId);

            // 🚀 2. 본문 조립 (4번지~) : 제너레이터에게 4번지부터 알맹이를 쓰라고 공간(Slice)을 넘겨줌
            movePacket.Serialize(span.Slice(4));

            return sendBuffer;
        }

        static byte[] MakeLoginPacket(string token)
        {
            byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
            ushort stringLen = (ushort)tokenBytes.Length;

            // 헤더(4) + 문자열 길이(2) + 문자열 데이터(N)
            ushort packetSize = (ushort)(4 + 2 + stringLen);
            ushort packetId = (ushort)PacketID.C_Login;

            byte[] buffer = new byte[packetSize];
            Span<byte> span = buffer.AsSpan();

            // 🚀 1. 헤더 조립 (0~3번지)
            BitConverter.TryWriteBytes(span.Slice(0, 2), packetSize);
            BitConverter.TryWriteBytes(span.Slice(2, 2), packetId);

            // 🚀 2. 본문(Payload) 조립 (4번지~)
            // 서버 핸들러에서는 앞 4바이트가 잘린 채로 받기 때문에, 
            // 여기서 4~5번지에 적은 문자열 길이가 서버 입장에서는 0~1번지가 되어 완벽하게 파싱됩니다!
            BitConverter.TryWriteBytes(span.Slice(4, 2), stringLen); // 4~5번지: 문자열 길이 기록
            tokenBytes.CopyTo(span.Slice(6));                        // 6번지~ : 실제 문자열 데이터 복사

            return buffer;
        }

        static async Task SendLoginPacketAsync(Socket client, string token)
        {
            byte[] packet = MakeLoginPacket(token);
            await client.SendAsync(packet, SocketFlags.None);
            Console.WriteLine($"[Send] C_Login - {packet.Length} bytes 전송");
        }


        // 2. [비동기 함수] 완성된 패킷을 전송만 하는 메서드
        static async Task SendMovePacketAsync(Socket client, float x, float y, float z)
        {
            // 수정중
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


        static async Task<string> LoginToAuthServerAsync(string username, string password)
        {
            using HttpClient httpClient = new HttpClient();
            var loginData = new { Username = username, Password = password };

            try
            {
                // C# 최신 문법: 클래스 없이도 익명 객체를 바로 JSON으로 쏴줍니다.
                HttpResponseMessage response = await httpClient.PostAsJsonAsync(AUTH_API_URL, loginData);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();

                    // JSON 데이터 까보기
                    using JsonDocument doc = JsonDocument.Parse(jsonResponse);
                    string token = doc.RootElement.GetProperty("token").GetString();
                    int playerId = doc.RootElement.GetProperty("playerId").GetInt32();

                    Console.WriteLine($"[Web] 로그인 성공! 발급받은 PlayerID: {playerId}");
                    Console.WriteLine($"[Web] JWT 토큰: {token.Substring(0, 20)}...");

                    return token;
                }
                else
                {
                    Console.WriteLine($"[Web] 로그인 실패. 상태 코드: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Web] Auth.API 서버에 연결할 수 없습니다. 켜져 있는지 확인하세요! ({ex.Message})");
                return null;
            }
        }

        private static string GenerateTestUserName()
        {
            string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            Random random = new Random();
            return new string(Enumerable.Repeat(chars, 8)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
