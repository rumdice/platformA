using Microsoft.AspNetCore.SignalR.Client;
using PlatformA.Library.Common;
using PlatformA.Library.Packets;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;

namespace PlatformA.Game.DummyClient.Scenarios
{
    public class MatchingScenario
    {
        // 🚀 서버 주소 세팅 (포트 번호를 현재 환경에 맞게 꼭 확인하세요!)
        
        public static async Task RunAsync()
        {
            Console.Clear();
            Console.WriteLine("==================================================");
            Console.WriteLine("   🎮 PlatformA 인터랙티브 게임 클라이언트 🎮");
            Console.WriteLine("==================================================\n");

            using var httpClient = new HttpClient();

            // 1️⃣ [수동 로그인] 유저 정보 입력받기
            Console.Write("👉 아이디(닉네임)를 입력하세요: ");
            string username = Console.ReadLine();
            Console.Write("👉 비밀번호를 입력하세요: ");
            string password = Console.ReadLine();

            Console.WriteLine("\n[1. 인증] Auth.API 에 로그인을 시도합니다...");
            string jwtToken = await LoginToAuthServerAsync(httpClient, username, password);
            if (string.IsNullOrEmpty(jwtToken)) return;

            // HttpClient 헤더에 토큰 장착
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);

            // 2️⃣ [대기열 진입] Ticketing.API
            Console.WriteLine("\n[2. 대기열] Ticketing 서버에 진입합니다...");
            var enterRes = await httpClient.PostAsync($"{Consts.TICKET_API_URL}/api/queue/enter", null);
            if (!enterRes.IsSuccessStatusCode)
            {
                Console.WriteLine("🚨 대기열 진입 실패!"); return;
            }

            // 3️⃣ [스마트 폴링] 대기열 대기 (루프)
            while (true)
            {
                var statusRes = await httpClient.GetAsync($"{Consts.TICKET_API_URL}/api/queue/status");
                if (!statusRes.IsSuccessStatusCode) return;

                var statusData = await statusRes.Content.ReadFromJsonAsync<QueueResponse>();
                if (statusData != null && statusData.Status == "Active")
                {
                    Console.WriteLine("✅ [통과] 대기열을 통과하여 입장 허가를 받았습니다!\n");
                    break; // 통과! 루프 탈출
                }

                Console.WriteLine($"⏳ 대기 중... (앞에 {statusData?.Rank}명 대기 / {statusData?.NextPollDelay}ms 뒤 재확인)");
                await Task.Delay(statusData?.NextPollDelay ?? 3000);
            }

            // 4️⃣ [게임 서버 접속] TCP 로비(1번 방) 연결
            Console.WriteLine("[3. 로비 입장] 게임 서버(TCP) 광장에 접속합니다...");
            using Socket tcpClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await tcpClient.ConnectAsync(Consts.GAME_SERVER_IP, Consts.GAME_SERVER_PORT);

            // 수신 루프 백그라운드 실행
            _ = ReceiveLoopAsync(tcpClient);

            // 로그인 패킷(토큰) 전송 -> 서버에서 1번 방(광장)으로 넣어줌
            await SendLoginPacketAsync(tcpClient, jwtToken);
            Console.WriteLine("🟢 게임 서버 로비에 성공적으로 입장했습니다!\n");

            // 5️⃣ [인터랙티브 조작] 매칭 시스템 연동
            await HandleUserInputAsync(httpClient, tcpClient, jwtToken);
        }

        // --- 유저 키보드 입력 핸들러 ---
        private static async Task HandleUserInputAsync(HttpClient httpClient, Socket tcpClient, string jwtToken)
        {
            HubConnection matchHub = null;

            try
            {
                while (true)
                {
                    Console.WriteLine("--------------------------------------------------");
                    Console.WriteLine(" [행동 선택] 'm': 매칭 큐 등록 | 'q': 게임 종료");
                    Console.WriteLine("--------------------------------------------------");
                    string input = Console.ReadLine()?.ToLower();

                    if (input == "q")
                    {
                        Console.WriteLine("게임을 종료합니다.");
                        break;
                    }
                    else if (input == "m")
                    {
                        Console.WriteLine("\n⚔️ 매칭 서버(SignalR)에 연결 중...");

                        // 매칭 Hub 연결 (아직 안 되어 있다면)
                        if (matchHub == null || matchHub.State == HubConnectionState.Disconnected)
                        {
                            matchHub = new HubConnectionBuilder()
                                .WithUrl(Consts.MATCH_HUB_URL, options => { options.AccessTokenProvider = () => Task.FromResult(jwtToken); })
                                .Build();

                            // 🎉 매칭 성공 이벤트 등록
                            matchHub.On<MatchSuccessEvent>("MatchFound", (matchInfo) =>
                            {
                                Console.WriteLine($"\n🔥🔥 [매칭 성사!] 🔥🔥");
                                Console.WriteLine($"👉 배정받은 투기장(방) 번호: {matchInfo.RoomId}");
                                Console.WriteLine($"👉 함께할 유저들: {string.Join(", ", matchInfo.MatchedUserIds)}\n");
                                // TODO: 나중에는 여기서 새로운 TCP 패킷(C_EnterRoom)을 쏴서 실제 방을 이동해야 합니다!
                            });

                            await matchHub.StartAsync();
                        }

                        // HTTP API로 매칭 큐 등록 요청
                        var matchRes = await httpClient.PostAsync(Consts.MATCH_API_URL, null);
                        if (matchRes.IsSuccessStatusCode)
                        {
                            Console.WriteLine("⏳ 매칭 큐에 등록되었습니다! 다른 유저를 기다립니다...\n");
                        }
                        else
                        {
                            Console.WriteLine($"🚨 매칭 요청 실패: {matchRes.StatusCode}");
                        }
                    }
                }
            }
            finally
            {
                if (matchHub != null)
                {
                    await matchHub.StopAsync();
                    await matchHub.DisposeAsync();
                    Console.WriteLine("매칭 허브 연결이 종료되었습니다.");
                }
            }
            
        }

        // --- 아래는 기존에 쓰시던 헬퍼 함수들을 그대로 가져옵니다 ---
        static async Task<string> LoginToAuthServerAsync(HttpClient client, string username, string password)
        {
            var loginData = new { Username = username, Password = password };
            var response = await client.PostAsJsonAsync(Consts.AUTH_API_URL, loginData);
            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(jsonResponse);
                int playerId = doc.RootElement.GetProperty("playerId").GetInt32();
                string token = doc.RootElement.GetProperty("token").GetString();
                Console.WriteLine($"✅ 로그인 성공! (PlayerID: {playerId})");
                return token;
            }
            return null;
        }

        static byte[] MakeLoginPacket(string token)
        {
            byte[] tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);
            ushort stringLen = (ushort)tokenBytes.Length;
            ushort packetSize = (ushort)(4 + 2 + stringLen);
            ushort packetId = (ushort)PacketID.C_Login;

            byte[] buffer = new byte[packetSize];
            Span<byte> span = buffer.AsSpan();
            BitConverter.TryWriteBytes(span.Slice(0, 2), packetSize);
            BitConverter.TryWriteBytes(span.Slice(2, 2), packetId);
            BitConverter.TryWriteBytes(span.Slice(4, 2), stringLen);
            tokenBytes.CopyTo(span.Slice(6));
            return buffer;
        }

        static async Task SendLoginPacketAsync(Socket client, string token)
        {
            byte[] packet = MakeLoginPacket(token);
            await client.SendAsync(packet, SocketFlags.None);
        }

        static async Task ReceiveLoopAsync(Socket client)
        {
            byte[] buffer = new byte[1024];
            try
            {
                while (true)
                {
                    int received = await client.ReceiveAsync(buffer, SocketFlags.None);
                    if (received == 0) break;
                    // (TCP 브로드캐스팅 수신 로직은 필요에 따라 추가하세요)
                }
            }
            catch { /* 정상 종료 무시 */ }
        }
    }
}