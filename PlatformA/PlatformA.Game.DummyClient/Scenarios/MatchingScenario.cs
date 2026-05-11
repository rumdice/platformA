using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Microsoft.AspNetCore.SignalR.Client;
using PlatformA.Library.Common;
using PlatformA.Library.Packets;

namespace PlatformA.Game.DummyClient.Scenarios
{
    public class MatchingScenario
    {
        public static async Task RunAsync()
        {
            Console.Clear();
            Console.WriteLine("==================================================");
            Console.WriteLine("   PlatformA 인터랙티브 게임 클라이언트");
            Console.WriteLine("==================================================\n");

            using var httpClient = new HttpClient();

            // 1. [로그인] 유저 정보 입력
            Console.Write("아이디(닉네임)를 입력하세요: ");
            string username = Console.ReadLine() ?? "";
            Console.Write("비밀번호를 입력하세요: ");
            string password = Console.ReadLine() ?? "";

            Console.WriteLine("\n[1. 인증] Auth.API 에 로그인을 시도합니다...");
            var session = await AuthHelper.LoginAsync(httpClient, username, password);
            if (session == null)
            {
                Console.WriteLine("로그인 실패. 서버 상태를 확인하세요.");
                return;
            }
            Console.WriteLine($"로그인 성공! (PlayerID: {session.PlayerId})");
            AuthHelper.ApplyToken(httpClient, session);

            // 2. [대기열 진입] Ticketing.API
            Console.WriteLine("\n[2. 대기열] Ticketing 서버에 진입합니다...");
            var enterRes = await httpClient.PostAsync($"{Consts.TICKET_API_URL}/api/queue/enter", null);
            if (enterRes.StatusCode == HttpStatusCode.Unauthorized)
            {
                session = await TryRefreshOrExitAsync(httpClient, session);
                if (session == null)
                    return;
                enterRes = await httpClient.PostAsync($"{Consts.TICKET_API_URL}/api/queue/enter", null);
            }
            if (!enterRes.IsSuccessStatusCode)
            {
                Console.WriteLine("대기열 진입 실패!");
                return;
            }

            // 3. [스마트 폴링] 대기열 대기
            while (true)
            {
                var statusRes = await httpClient.GetAsync($"{Consts.TICKET_API_URL}/api/queue/status");
                if (statusRes.StatusCode == HttpStatusCode.Unauthorized)
                {
                    session = await TryRefreshOrExitAsync(httpClient, session);
                    if (session == null)
                        return;
                    statusRes = await httpClient.GetAsync($"{Consts.TICKET_API_URL}/api/queue/status");
                }
                if (!statusRes.IsSuccessStatusCode)
                    return;

                var statusData = await statusRes.Content.ReadFromJsonAsync<QueueStatusDto>();
                if (statusData?.Status == "Active")
                {
                    Console.WriteLine("[통과] 대기열을 통과하여 입장 허가를 받았습니다!\n");
                    break;
                }
                Console.WriteLine($"대기 중... (앞에 {statusData?.Rank}명 대기 / {statusData?.NextPollDelay}ms 뒤 재확인)");
                await Task.Delay(statusData?.NextPollDelay ?? 3000);
            }

            // 4. [게임 서버 접속] TCP 로비(1번 방) 연결
            Console.WriteLine("[3. 로비 입장] 게임 서버(TCP) 광장에 접속합니다...");
            using Socket tcpClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await tcpClient.ConnectAsync(Consts.GAME_SERVER_IP, Consts.GAME_SERVER_PORT);
            _ = ReceiveLoopAsync(tcpClient);
            await tcpClient.SendAsync(MakeLoginPacket(session.AccessToken, roomId: 1), SocketFlags.None);
            Console.WriteLine("게임 서버 로비에 성공적으로 입장했습니다!\n");

            // 5. [인터랙티브 조작] 매칭 시스템 연동
            await HandleUserInputAsync(httpClient, tcpClient, session);
        }

        // --- 유저 키보드 입력 핸들러 ---
        private static async Task HandleUserInputAsync(
            HttpClient httpClient, Socket tcpClient, TokenSession initialSession)
        {
            var session = initialSession;
            HubConnection? matchHub = null;

            try
            {
                while (true)
                {
                    Console.WriteLine("--------------------------------------------------");
                    Console.WriteLine(" [행동 선택] 'm': 매칭 큐 등록 | 'q': 게임 종료");
                    Console.WriteLine("--------------------------------------------------");
                    string input = Console.ReadLine()?.ToLower() ?? "";

                    if (input == "q")
                    {
                        Console.WriteLine("게임을 종료합니다.");
                        break;
                    }

                    if (input == "m")
                    {
                        Console.WriteLine("\n매칭 서버(SignalR)에 연결 중...");

                        if (matchHub == null || matchHub.State == HubConnectionState.Disconnected)
                        {
                            matchHub = new HubConnectionBuilder()
                                .WithUrl(Consts.MATCH_HUB_URL, options =>
                                {
                                    options.AccessTokenProvider = () => Task.FromResult<string?>(session.AccessToken);
                                })
                                .Build();

                            matchHub.On<MatchSuccessEvent>("MatchFound", async (matchInfo) =>
                            {
                                Console.WriteLine($"\n[매칭 성사!] 배정 방 번호: {matchInfo.RoomId}");
                                Console.WriteLine($"함께할 유저들: {string.Join(", ", matchInfo.MatchedUserIds)}\n");

                                await tcpClient.SendAsync(MakeEnterRoomPacket(matchInfo.RoomId), SocketFlags.None);
                            });

                            await matchHub.StartAsync();
                        }

                        var matchRes = await httpClient.PostAsync(Consts.MATCH_API_URL, null);
                        if (matchRes.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            var newSession = await TryRefreshOrExitAsync(httpClient, session);
                            if (newSession == null)
                                break;
                            session = newSession;
                            matchRes = await httpClient.PostAsync(Consts.MATCH_API_URL, null);
                        }

                        if (matchRes.IsSuccessStatusCode)
                            Console.WriteLine("매칭 큐에 등록되었습니다! 다른 유저를 기다립니다...\n");
                        else
                            Console.WriteLine($"매칭 요청 실패: {matchRes.StatusCode}");
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

        // 401 수신 시 refresh 시도. 실패 시 메시지 출력 후 null 반환.
        private static async Task<TokenSession?> TryRefreshOrExitAsync(HttpClient http, TokenSession session)
        {
            Console.WriteLine("[401] Access Token 만료 → Refresh 시도...");
            var newSession = await AuthHelper.TryRefreshAsync(http, session);
            if (newSession == null)
            {
                Console.WriteLine("세션이 만료되었습니다. 재로그인이 필요합니다.");
                return null;
            }
            AuthHelper.ApplyToken(http, newSession);
            Console.WriteLine("[OK] 토큰 갱신 완료.");
            return newSession;
        }

        // --- 헬퍼 함수 ---

        static byte[] MakeLoginPacket(string token, int roomId = 1)
            => PacketHelper.BuildPacket(new Packet { CLogin = new CLogin { RoomId = roomId, JwtToken = token } });

        static byte[] MakeEnterRoomPacket(int roomId)
            => PacketHelper.BuildPacket(new Packet { CEnterRoom = new CEnterRoom { RoomId = roomId } });

        static async Task ReceiveLoopAsync(Socket client)
        {
            try
            {
                while (true)
                {
                    byte[]? frame = await PacketHelper.ReceiveFrameAsync(client);
                    if (frame == null)
                        break;

                    try
                    {
                        Packet envelope = PacketHelper.ParseEnvelope(frame);
                        switch (envelope.PayloadCase)
                        {
                            case Packet.PayloadOneofCase.SLogin:
                                SLogin resp = envelope.SLogin;
                                Console.WriteLine(resp.ResultCode == LoginResultCode.LoginSuccess
                                    ? $"[TCP 로그인 성공] PlayerId: {resp.PlayerId}"
                                    : $"[TCP 로그인 실패] ResultCode: {resp.ResultCode}");
                                break;
                            case Packet.PayloadOneofCase.SEnterRoom:
                                SEnterRoom enterResp = envelope.SEnterRoom;
                                Console.WriteLine(enterResp.ResultCode == EnterRoomResultCode.EnterRoomSuccess
                                    ? $"[방 이동 성공] {enterResp.RoomId}번 게임방에 입장했습니다!"
                                    : $"[방 이동 실패] ResultCode: {enterResp.ResultCode}");
                                break;
                            case Packet.PayloadOneofCase.SMove:
                                SMove move = envelope.SMove;
                                Console.WriteLine($"[S_Move] Player {move.PlayerId} → ({move.X:F1}, {move.Y:F1}, {move.Z:F1})");
                                break;
                        }
                    }
                    catch { }
                }
            }
            catch { /* 정상 종료 무시 */ }
        }
    }
}
