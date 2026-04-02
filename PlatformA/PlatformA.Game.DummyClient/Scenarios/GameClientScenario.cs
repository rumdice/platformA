using Microsoft.AspNetCore.SignalR.Client;
using PlatformA.Library.Common;
using PlatformA.Library.Packets;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace PlatformA.Game.DummyClient.Scenarios
{

    public class GameClientScenario
    {
        

        public static async Task RunAsync()
        {
            Console.Clear();
            Console.WriteLine("=== 👾 게임 서버 인터랙티브 클라이언트 ===");

            // 기존 DummyClient 안의 Main 함수에 있던 
            // Auth.API 로그인 -> 소켓 연결 -> C_Move 발송 루프 로직을 여기에 넣습니다.

            Console.WriteLine("=== 👾 Dummy Client for Pipeline Server ===");
            await Task.Delay(1000); // 서버가 켜질 시간 확보

            // =================================================================
            // 🌐 [STEP 1] Auth.API 로그인 (JWT 발급)
            // =================================================================
            Console.WriteLine("[Web] Auth.API 에 로그인을 시도합니다...");
            var userName = GenerateTestUserName();
            string realToken = await LoginToAuthServerAsync(userName, "1234");

            if (string.IsNullOrEmpty(realToken))
            {
                Console.WriteLine("[Web] 로그인 실패! 프로그램을 종료합니다.");
                return;
            }

            // =================================================================
            // 🎮 [STEP 2] 매칭 서버(SignalR) 접속 및 대기
            // =================================================================
            // 🚨 여기서 게임 서버(TCP) 접속 코드는 일단 지우거나 아래로 내립니다!
            // 대신 매칭을 먼저 시작합니다.
            await StartMatchingAsync(realToken);


            //Console.WriteLine("연결이 종료되었습니다. 엔터를 누르면 메뉴로 돌아갑니다.");
            //Console.ReadLine();
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
            // 헤더(4) + 문자열 길이(2) + 문자열 데이터(N)
            ushort packetSize = (ushort)(4 + 2 + stringLen);
            ushort packetId = (ushort)PacketID.C_Login;

            byte[] buffer = new byte[packetSize];
            Span<byte> span = buffer.AsSpan();

            // 🚀 1. 헤더 조립 (0~3번지)
            BitConverter.TryWriteBytes(span.Slice(0, 2), packetSize);
            BitConverter.TryWriteBytes(span.Slice(2, 2), packetId);

            //// 🚀 2. RoomId 기록 (4~7)
            //BitConverter.TryWriteBytes(span.Slice(4, 4), roomId);

            //// 3. 문자열 길이 기록 (8~9번지)
            //BitConverter.TryWriteBytes(span.Slice(8, 2), stringLen);

            // 4. 토큰 데이터 기록 (10번지~)
            //tokenBytes.CopyTo(span.Slice(10));


            // 🚀 문자열 길이 기록 (4~5번지)
            BitConverter.TryWriteBytes(span.Slice(4, 2), stringLen);

            // 🚀 토큰 데이터 기록 (6번지~)
            tokenBytes.CopyTo(span.Slice(6));

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
                HttpResponseMessage response = await httpClient.PostAsJsonAsync(Consts.AUTH_API_URL, loginData);

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


        static async Task StartMatchingAsync(string jwtToken)
        {
            Console.WriteLine("[매칭] 매칭 서버(SignalR)에 연결을 시도합니다...");

            // 1. 매칭 서버의 Hub URL 설정 (포트는 Matching.API 설정에 맞게 수정하세요)
          
            var hubConnection = new HubConnectionBuilder()
                .WithUrl(Consts.MATCH_HUB_URL, options =>
                {
                    // SignalR 연결 시 JWT 토큰을 함께 보냅니다 (인증용)
                    options.AccessTokenProvider = () => Task.FromResult(jwtToken);
                })
                .Build();

            // 🚀 2. 매칭 성사 시 서버가 호출해 줄 콜백 함수 등록 ("MatchFound" 이벤트 대기)
            hubConnection.On<MatchSuccessEvent>("MatchFound", async (matchInfo) =>
            {
                Console.WriteLine($"\n🎉 [SignalR 수신] 매칭 성공!! 배정받은 방 번호: {matchInfo.RoomId}");
                Console.WriteLine($"🎉 [SignalR 수신] 함께할 유저들: {string.Join(", ", matchInfo.MatchedUserIds)}");

                // 연결 끊기 (매칭이 끝났으므로)
                await hubConnection.StopAsync();

                // 🚀 3. 이제 발급받은 RoomId를 들고 실제 TCP 게임 서버로 접속합니다!
                Console.WriteLine($"[이동] {matchInfo.RoomId}번 방으로 게임 서버 접속을 시작합니다...\n");
                await ConnectAndPlayGameAsync(jwtToken, matchInfo.RoomId);
            });

            try
            {
                // Hub 연결 시작
                await hubConnection.StartAsync();
                Console.WriteLine("[매칭] 서버 연결 완료. 대기열에 진입합니다...");

                // 메칭 요청을 http request 로 처리는 백그라운드로 디커플링
                using HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);
                var response = await client.PostAsync(Consts.MATCH_API_URL, null); // 넘길 본문 데이터는 없음(토큰에 다 있음)

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("[매칭] 큐 진입 완료! 상대를 기다립니다...");
                }
                else
                {
                    Console.WriteLine($"[매칭 에러] HTTP API 요청 실패: {response.StatusCode}");
                }

                await Task.Delay(-1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[매칭 에러] {ex.Message}");
            }
        }


        /// <summary>
        /// 게임서버 접속
        /// </summary>
        public static async Task ConnectAndLoginAsync(int userId, string realToken)
        {
            // =================================================================
            // 🎮 [STEP 2] 게임 서버(Game Server)에 TCP 소켓으로 접속하기
            // =================================================================

            using Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                await client.ConnectAsync(Consts.GAME_SERVER_IP, Consts.GAME_SERVER_PORT);
                
                _ = ReceiveLoopAsync(client);

                // JWT 토큰으로 C_Login 패킷 전송 (게임 서버가 TokenManager로 검증)
                await SendLoginPacketAsync(client, realToken);

                // *나중에는 C_Login 패킷에 roomId도 같이 담아 보내서 해당 방에 들어가게 해야 합니다!*
                await Task.Delay(500);

                Console.WriteLine($"[UserID : {userId}] 게임 서버 접속 성공 엔터를 누를 때마다 이동 패킷(C_Move)을 서버로 전송합니다. q로 종료");
                Random rand = new Random();

                while (true)
                {
                    string input = Console.ReadLine();
                    if (input?.ToLower() == "q") break;

                    float x = rand.Next(-50, 50);
                    float y = rand.Next(-50, 50);
                    await SendMovePacketAsync(client, x, y, 0f);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"접속 실패: {ex.Message}");
            }
        }


        /// <summary>
        /// 게임서버 접속 (매칭룸 정보 포함)
        /// </summary>
        static async Task ConnectAndPlayGameAsync(string realToken, int roomId)
        {
            // =================================================================
            // 🎮 [STEP 2] 게임 서버(Game Server)에 TCP 소켓으로 접속하기
            // =================================================================


            using Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                await client.ConnectAsync(Consts.GAME_SERVER_IP, Consts.GAME_SERVER_PORT);
                Console.WriteLine("게임 서버 접속 성공!\n");

                _ = ReceiveLoopAsync(client);

                // JWT 토큰으로 C_Login 패킷 전송 (게임 서버가 TokenManager로 검증)
                await SendLoginPacketAsync(client, realToken);

                // *나중에는 C_Login 패킷에 roomId도 같이 담아 보내서 해당 방에 들어가게 해야 합니다!*
                await Task.Delay(500);

                Console.WriteLine("엔터를 누를 때마다 이동 패킷(C_Move)을 서버로 전송합니다.");
                Random rand = new Random();

                while (true)
                {
                    string input = Console.ReadLine();
                    if (input?.ToLower() == "q") break;

                    float x = rand.Next(-50, 50);
                    float y = rand.Next(-50, 50);
                    await SendMovePacketAsync(client, x, y, 0f);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"접속 실패: {ex.Message}");
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
