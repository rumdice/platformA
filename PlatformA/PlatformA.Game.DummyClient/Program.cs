using Microsoft.IdentityModel.Tokens;
using PlatformA.Library.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace PlatformA.Game.DummyClient
{

    internal class Program
    {
        
        // 🚀 방금 띄운 Auth.API의 실제 주소 (포트 번호를 Swagger 창에 뜬 번호로 꼭 바꿔주세요!)
        private const string AUTH_API_URL = "https://localhost:7088/api/Auth/login";

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== 👾 Dummy Client for Pipeline Server ===");
            await Task.Delay(1000); // 서버가 켜질 시간 확보

            // =================================================================
            // 🌐 [STEP 1] 웹 서버(Auth.API)에 HTTP POST로 로그인 요청하기
            // =================================================================
            Console.WriteLine("[Web] Auth.API 에 로그인을 시도합니다...");
            string realToken = await LoginToAuthServerAsync("test", "1234");

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

        static byte[] MakeLoginPacket(string token)
        {
            byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
            ushort stringLen = (ushort)tokenBytes.Length;

            // 헤더(4) + 문자열 길이(2) + 문자열 데이터(N)
            ushort packetSize = (ushort)(4 + 2 + stringLen);
            ushort packetId = (ushort)PacketID.C_Login;

            byte[] buffer = new byte[packetSize];
            Span<byte> span = buffer.AsSpan();

            // 1. 헤더 조립
            BitConverter.TryWriteBytes(span.Slice(0, 2), packetSize);
            BitConverter.TryWriteBytes(span.Slice(2, 2), packetId);

            // 2. 본문(Payload) 조립
            BitConverter.TryWriteBytes(span.Slice(4, 2), stringLen); // 문자열 길이 먼저 씀
            tokenBytes.CopyTo(span.Slice(6));                        // 실제 문자열 데이터 복사

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


        // =========================================================================
        // JWT 토큰 생성기 (테스트용)
        // =========================================================================
        static string GenerateTestToken(int playerId)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(Consts.SECRET_KEY);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                // 토큰 안에 "이 토큰의 주인은 playerId 다" 라고 기록 (Claim)
                Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, playerId.ToString()) }),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
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

    }
}