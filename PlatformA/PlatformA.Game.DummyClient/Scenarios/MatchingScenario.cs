using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using PlatformA.Library.Common;

namespace PlatformA.Game.DummyClient.Scenarios
{
    public class MatchingScenario
    {
        public static async Task RunAsync()
        {
            try
            { Console.Clear(); }
            catch { }
            Console.WriteLine("==================================================");
            Console.WriteLine("   PlatformA 인터랙티브 게임 클라이언트");
            Console.WriteLine("==================================================\n");

            using var httpClient = new HttpClient();

            // 1. [로그인]
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

            // 2. [대기열 진입]
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

            // 3. [대기열 통과 대기]
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

            // 4. [로비 연결] Game.Lobby SignalR 허브에 연결
            Console.WriteLine("[3. 로비 연결] Game.Lobby SignalR 허브에 접속합니다...");
            var lobbyHub = new HubConnectionBuilder()
                .WithUrl(Consts.LOBBY_HUB_URL, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(session.AccessToken);
                })
                .WithAutomaticReconnect()
                .Build();

            lobbyHub.On<object>("MatchQueued", data =>
            {
                Console.WriteLine("\n[매칭 대기 중] 다른 유저를 기다립니다...");
            });

            lobbyHub.On<object>("MatchFound", data =>
            {
                Console.WriteLine($"\n[매칭 성사!] 게임 서버 정보: {data}");
            });

            lobbyHub.On<object>("MatchError", data =>
            {
                Console.WriteLine($"\n[매칭 오류] {data}");
            });

            lobbyHub.On<object>("MatchCancelled", data =>
            {
                Console.WriteLine("\n[취소 완료] 매칭이 취소되었습니다.");
            });

            try
            {
                await lobbyHub.StartAsync();
                Console.WriteLine("로비 허브 연결 성공!\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"로비 연결 실패: {ex.Message}");
                return;
            }

            // 5. [인터랙티브 조작]
            try
            {
                while (true)
                {
                    Console.WriteLine("--------------------------------------------------");
                    Console.WriteLine(" [행동 선택] 'm': 매칭 신청 | 'c': 매칭 취소 | 'q': 종료");
                    Console.WriteLine("--------------------------------------------------");
                    string input = Console.ReadLine()?.ToLower() ?? "";

                    if (input == "q")
                    {
                        Console.WriteLine("게임을 종료합니다.");
                        break;
                    }

                    if (input == "m")
                    {
                        Console.WriteLine("\n매칭 신청 중 (Lobby SignalR → Matching.API)...");
                        await lobbyHub.InvokeAsync("RequestMatch", "gomoku");
                    }

                    if (input == "c")
                    {
                        Console.WriteLine("\n매칭 취소 중...");
                        await lobbyHub.InvokeAsync("CancelMatch", "gomoku");
                    }
                }
            }
            finally
            {
                await lobbyHub.StopAsync();
                await lobbyHub.DisposeAsync();
                Console.WriteLine("로비 허브 연결이 종료되었습니다.");
            }
        }

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
    }
}
