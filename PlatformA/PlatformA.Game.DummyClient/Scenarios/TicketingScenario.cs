using System.Diagnostics;
using System.Net.Http.Json;
using PlatformA.Library.Common;

namespace PlatformA.Game.DummyClient.Scenarios
{

    public class TicketingScenario
    {
        private const int USER_COUNT = 200; // 2500명이 동시에 접속 시도

        public static async Task RunAsync()
        {
            Console.Clear();
            Console.WriteLine("=== 🚦 대기열(Netfunnel) 시스템 부하 시뮬레이터 ===");

            // 기존 Ticketing.Test 안의 Main 함수에 있던 
            // HttpClient 생성 및 200명 접속 루프 로직을 여기에 그대로 붙여넣습니다!

            // 0. 설정
            //using var client = new HttpClient();
            //client.Timeout = TimeSpan.FromSeconds(300); // 대기 시간 길어질 수 있으므로 타임아웃 늘림

            // 1. 초기화 (티켓 100장 발행 + 대기열 초기화는 수동 혹은 API 필요)
            // (주의: Redis에서 'del ticket:queue:global'로 대기열을 한번 비우고 시작하는 게 좋습니다)
            //Console.WriteLine("🔄 서버 초기화 요청...");
            //await client.PostAsync($"{TICKET_URL}/api/tickets/reset?count=100", null);
            //Console.WriteLine("✅ 티켓 100장 세팅 완료.\n");

            Console.WriteLine($"🔥 {USER_COUNT}명의 유저가 대기열 진입을 시도합니다...");
            var sw = Stopwatch.StartNew();

            var tasks = new List<Task<string>>();

            for (int i = 1; i < USER_COUNT + 1; i++)
            {
                int userId = i; // 캡처
                //tasks.Add(Task.Run(() => SimulateUserFlow(userId, TICKET_URL)));
                tasks.Add(Task.Run(() => SimulateUserTicketFlow(userId, Consts.TICKET_API_URL)));
                await Task.Delay(10); // 포트 고갈 방지용 잠깐 딜레이.
            }

            // 모든 유저의 시나리오가 끝날 때까지 대기
            var results = await Task.WhenAll(tasks);
            sw.Stop();

            // 결과 집계
            int successCount = results.Count(r => r == "SUCCESS");
            //int soldOutCount = results.Count(r => r == "SOLDOUT");
            int failCount = results.Count(r => r == "FAIL");

            Console.WriteLine($"\n--- 🏁 최종 통합 결과 리포트 ({sw.Elapsed.TotalSeconds:F1}초) ---");
            Console.WriteLine($"🟢 인게임 TCP 접속 성공: {successCount}명");
            Console.WriteLine($"❌ 에러/실패: {failCount}명");

            //Console.WriteLine($"\n--- 🏁 최종 결과 리포트 ({sw.Elapsed.TotalSeconds:F1}초) ---");
            //Console.WriteLine($"🎉 구매 성공 (대기열 통과 완료): {successCount}명");
            //Console.WriteLine($"🎫 매진 퇴장: {soldOutCount}명");
            //Console.WriteLine($"❌ 에러/실패: {failCount}명");


            Console.WriteLine("테스트가 종료되었습니다. 엔터를 누르면 메뉴로 돌아갑니다.");
            Console.ReadLine();
        }

        // --- [개별 유저 시나리오 함수] ---
        private static async Task<string> SimulateUserTicketFlow(int userId, string host)
        {
            using var client = new HttpClient();

            try
            {
                // =================================================================
                // 🌐 [STEP 1] Auth.API 로그인 (JWT 발급)
                // =================================================================
                Console.WriteLine("[Web] Auth.API 에 로그인을 시도합니다...");
                var session = await AuthHelper.LoginAsync(client, AuthHelper.GenerateTestUserName(), "123456");
                if (session == null)
                {
                    Console.WriteLine("[Web] 로그인 실패! 프로그램을 종료합니다.");
                    return "FAIL";
                }
                string realToken = session.AccessToken;
                AuthHelper.ApplyToken(client, session);


                // 🚀 2. STEP 2: 대기열 진입
                var enterRes = await client.PostAsync($"{host}/api/queue/enter", null);
                if (!enterRes.IsSuccessStatusCode)
                {
                    string errorMsg = await enterRes.Content.ReadAsStringAsync();
                    Console.WriteLine($"[대기열 진입 실패] User_{userId} - Status: {enterRes.StatusCode}, Msg: {errorMsg}");
                    return "FAIL";
                }

                // 🚀 3. STEP 3: 무한 대기 (SmartPolling) : 등수에 따라서 다음 폴링까지 대기시간이 달라짐.
                while (true)
                {
                    var statusRes = await client.GetAsync($"{host}/api/queue/status");
                    if (!statusRes.IsSuccessStatusCode)
                        return "FAIL";

                    var statusData = await statusRes.Content.ReadFromJsonAsync<QueueStatusDto>();
                    if (statusData != null && statusData.Status == "Active")
                    {
                        // 입장 허가! (문지기를 통과함)
                        Console.WriteLine($"✅ [User_{userId:D3}] 입장 허가 획득! (대기열 통과)");
                        break;
                    }
                    else
                    {
                        // 내 순번이 너무 많이 출력되면 콘솔이 터지므로, 10단위 유저만 로그를 찍어봅니다.
                        if (userId % 10 == 0)
                        {
                            Console.WriteLine($"⏳ [User_{userId:D3}] 대기 중... (앞에 {statusData?.Rank}명 대기 / {statusData?.NextPollDelay}ms 뒤 재확인)");
                        }

                        // 1초 뒤에 다시 물어보기 (실무에서는 3~5초 권장)
                        int delay = statusData?.NextPollDelay ?? 3000;
                        await Task.Delay(delay);
                    }
                }

                // 게임 서버 접속 (비-인터랙티브 검증)
                Console.WriteLine($"✅ [User_{userId:D3}] 🚀 인게임 TCP 서버로 접속을 시도합니다!");
                bool gameOk = await GameClientScenario.VerifyGameServerLoginAsync(realToken);
                Console.WriteLine(gameOk
                    ? $"✅ [User_{userId:D3}] 🎮 게임서버 로그인 성공!"
                    : $"❌ [User_{userId:D3}] 게임서버 로그인 실패");
                return gameOk ? "SUCCESS" : "FAIL";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERR [User_{userId}]: {ex.Message}");
                return "FAIL";
            }
        }


        // --- [개별 유저 시나리오 함수] ---
        private static async Task<string> SimulateUserFlow(int id, string host)
        {
            using var myClient = new HttpClient();
            string userId = $"user_{id}";

            try
            {
                // STEP 1: 대기열 진입 (Enter)
                var enterRes = await myClient.PostAsync($"{host}/api/queue/enter?userId={userId}", null);
                if (!enterRes.IsSuccessStatusCode)
                    return "FAIL";

                // STEP 2: 무한 대기 (Polling)
                while (true)
                {
                    var statusRes = await myClient.GetAsync($"{host}/api/queue/status?userId={userId}");

                    // 400 에러 등 처리
                    if (!statusRes.IsSuccessStatusCode)
                        return "FAIL";

                    var statusData = await statusRes.Content.ReadFromJsonAsync<QueueStatusDto>();

                    if (statusData.Status == "Pass")
                    {
                        // 입장 허가! 루프 탈출
                        // Console.WriteLine($"[User_{id}] 입장 성공! 구매 시도하러 갑니다.");
                        break;
                    }
                    else
                    {
                        // 아직 대기 중.. (로그가 너무 많으면 주석 처리)
                        // Console.WriteLine($"[User_{id}] 대기 중... (현재 {statusData.Rank}등)");

                        // 1초 뒤에 다시 확인 (서버 부하 방지)
                        await Task.Delay(1000);
                    }
                }

                // STEP 3: 구매 시도 (Buy)
                // 주의: QueryString으로 userId를 넘겨야 Controller가 검증 가능
                var buyRes = await myClient.PostAsync($"{host}/api/tickets/buy-final?_userId={userId}", null);
                var msg = await buyRes.Content.ReadAsStringAsync();

                if (buyRes.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✅ [User_{id}] 구매 성공!");
                    return "SUCCESS";
                }
                else if (msg.Contains("매진"))
                {
                    // Console.WriteLine($"😭 [User_{id}] 매진됨.");
                    return "SOLDOUT";
                }
                else
                {
                    Console.WriteLine($"❌ [User_{id}] 구매 실패 (코드: {buyRes.StatusCode}) - {msg}");
                    return "FAIL";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERR [User_{id}]: {ex.Message}");
                return "FAIL";
            }
        }

    }
}
