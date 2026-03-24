using System.Diagnostics;
using System.Net.Http.Json;

namespace PlatformA.Game.DummyClient.Scenarios
{

    // 응답 DTO
    class QueueResponse { 
        public string Status { get; set; }
        public long Rank { get; set; } 
    }


    public class TicketingScenario
    {
        public static async Task RunAsync()
        {
            Console.Clear();
            Console.WriteLine("=== 🚦 대기열(Netfunnel) 시스템 부하 시뮬레이터 ===");

            // 기존 Ticketing.Test 안의 Main 함수에 있던 
            // HttpClient 생성 및 200명 접속 루프 로직을 여기에 그대로 붙여넣습니다!

            Console.WriteLine("=== 🚦 대기열(Netfunnel) 시스템 시뮬레이터 ===");

            // 0. 설정
            string baseUrl = "http://localhost:5282"; // 포트번호 확인 (사용자 환경: 5282)
            int totalUserCount = 200; // 접속할 유저 수 (티켓 100장 vs 유저 200명)
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(300); // 대기 시간 길어질 수 있으므로 타임아웃 늘림

            // 1. 초기화 (티켓 100장 발행 + 대기열 초기화는 수동 혹은 API 필요)
            // (주의: Redis에서 'del queue:iu_concert'로 대기열을 한번 비우고 시작하는 게 좋습니다)
            Console.WriteLine("🔄 서버 초기화 요청...");
            await client.PostAsync($"{baseUrl}/api/tickets/reset?count=100", null);
            Console.WriteLine("✅ 티켓 100장 세팅 완료.\n");

            Console.WriteLine($"🔥 {totalUserCount}명의 유저가 대기열 진입을 시도합니다...");
            var sw = Stopwatch.StartNew();

            var tasks = new List<Task<string>>();

            for (int i = 0; i < totalUserCount; i++)
            {
                int userId = i; // 캡처
                tasks.Add(Task.Run(() => SimulateUserFlow(userId, baseUrl)));
            }

            // 모든 유저의 시나리오가 끝날 때까지 대기
            var results = await Task.WhenAll(tasks);
            sw.Stop();

            // 결과 집계
            int successCount = results.Count(r => r == "SUCCESS");
            int soldOutCount = results.Count(r => r == "SOLDOUT");
            int failCount = results.Count(r => r == "FAIL");

            Console.WriteLine($"\n--- 🏁 최종 결과 리포트 ({sw.Elapsed.TotalSeconds:F1}초) ---");
            Console.WriteLine($"🎉 구매 성공: {successCount}명");
            Console.WriteLine($"🎫 매진 퇴장: {soldOutCount}명");
            Console.WriteLine($"❌ 에러/실패: {failCount}명");


            Console.WriteLine("테스트가 종료되었습니다. 엔터를 누르면 메뉴로 돌아갑니다.");
            Console.ReadLine();
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
                if (!enterRes.IsSuccessStatusCode) return "FAIL";

                // STEP 2: 무한 대기 (Polling)
                while (true)
                {
                    var statusRes = await myClient.GetAsync($"{host}/api/queue/status?userId={userId}");

                    // 400 에러 등 처리
                    if (!statusRes.IsSuccessStatusCode) return "FAIL";

                    var statusData = await statusRes.Content.ReadFromJsonAsync<QueueResponse>();

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
