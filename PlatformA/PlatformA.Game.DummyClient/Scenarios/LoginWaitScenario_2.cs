using System.Net;
using System.Net.Http.Json;
using PlatformA.Library.Common;

namespace PlatformA.Game.DummyClient.Scenarios
{
    /// <summary>
    /// 시나리오 7: 단일 유저 로그인/재로그인/대기열 통합 테스트
    ///
    /// 검증 항목:
    ///   [A] 재로그인 시 구 Refresh Token 자동 폐기 (단일 세션 정책)
    ///   [B] Refresh Token → 새 Access Token + 새 Refresh Token 발급 (토큰 로테이션)
    ///   [C] 갱신된 토큰으로 대기열 API 정상 호출
    ///   [D] 대기열 Active 대기 중 401 발생 시 자동 갱신 + 재시도
    /// </summary>
    public class LoginWaitScenario_2
    {
        public static async Task RunAsync()
        {
            try { Console.Clear(); } catch { }
            PrintHeader();

            using var http = new HttpClient();

            // ── [Step 1] 계정 입력 ────────────────────────────────
            Console.Write("  아이디를 입력하세요: ");
            string username = Console.ReadLine()?.Trim() ?? "";
            Console.Write("  비밀번호를 입력하세요: ");
            string password = Console.ReadLine()?.Trim() ?? "";
            Console.WriteLine();

            // ── [Step 2] 1차 로그인 ───────────────────────────────
            PrintStep("Step 2", "1차 로그인");
            var session1 = await AuthHelper.LoginAsync(http, username, password);
            if (session1 == null)
            {
                Fail("로그인 실패. 서버 상태 및 계정 정보를 확인하세요.");
                WaitEnter();
                return;
            }
            Console.WriteLine($"  [OK] PlayerId={session1.PlayerId}");
            Console.WriteLine($"       AccessToken  : ...{Tail(session1.AccessToken)}");
            Console.WriteLine($"       RefreshToken : {Head(session1.RefreshToken)}...");

            // ── [Step 3] 재로그인 (동일 계정) ────────────────────
            PrintStep("Step 3", "재로그인 (동일 계정으로 다시 로그인)");
            var session2 = await AuthHelper.LoginAsync(http, username, password);
            if (session2 == null)
            {
                Fail("재로그인 실패.");
                WaitEnter();
                return;
            }
            Console.WriteLine($"  [OK] 새 세션 발급 완료.");
            Console.WriteLine($"       새 RefreshToken: {Head(session2.RefreshToken)}...");

            // ── [Test A] 구 Refresh Token 무효화 확인 ─────────────
            PrintStep("Test A", "구 Refresh Token 재사용 차단 검증");
            var staleSession = await AuthHelper.TryRefreshAsync(http, session1);
            if (staleSession == null)
                Pass("구 Refresh Token은 정상적으로 거부됩니다. (단일 세션 정책 동작 확인)");
            else
                Fail("구 Refresh Token이 여전히 유효합니다! Redis 키 구조를 확인하세요.");

            // ── [Test B] Refresh Token → 새 토큰 갱신 ───────────
            PrintStep("Test B", "Refresh Token으로 새 Access Token 발급 (토큰 로테이션)");
            var session3 = await AuthHelper.TryRefreshAsync(http, session2);
            if (session3 == null)
            {
                Fail("Token 갱신 실패. 서버 상태를 확인하세요.");
                WaitEnter();
                return;
            }
            Console.WriteLine($"  [OK] 갱신 완료.");
            Console.WriteLine($"       새 AccessToken  : ...{Tail(session3.AccessToken)}");
            Console.WriteLine($"       새 RefreshToken : {Head(session3.RefreshToken)}...");

            // session2의 RefreshToken은 로테이션으로 폐기됨 — 추가 검증
            var rotatedSession = await AuthHelper.TryRefreshAsync(http, session2);
            if (rotatedSession == null)
                Pass("로테이션된 구 Refresh Token도 정상 차단됩니다.");
            else
                Fail("로테이션 후 구 Refresh Token이 여전히 유효합니다!");

            // ── [Test C] 갱신된 토큰으로 대기열 진입 ─────────────
            PrintStep("Test C", "갱신된 Access Token으로 대기열 진입");
            AuthHelper.ApplyToken(http, session3);
            var currentSession = session3;

            var enterResp = await http.PostAsync($"{Consts.TICKET_API_URL}/api/queue/enter", null);
            if (enterResp.StatusCode == HttpStatusCode.Unauthorized)
            {
                Console.WriteLine("  [401] Access Token 만료 감지 → Refresh 시도...");
                currentSession = await AuthHelper.TryRefreshAsync(http, currentSession);
                if (currentSession == null)
                {
                    Fail("세션 만료. 재로그인이 필요합니다.");
                    WaitEnter();
                    return;
                }
                AuthHelper.ApplyToken(http, currentSession);
                Console.WriteLine("  [OK] 토큰 갱신 완료. 대기열 진입 재시도...");
                enterResp = await http.PostAsync($"{Consts.TICKET_API_URL}/api/queue/enter", null);
            }

            if (!enterResp.IsSuccessStatusCode)
            {
                string msg = await enterResp.Content.ReadAsStringAsync();
                Fail($"대기열 진입 실패: HTTP {(int)enterResp.StatusCode} — {msg}");
                WaitEnter();
                return;
            }
            Pass("갱신된 토큰으로 대기열 진입 성공!");

            // ── [Test D] 대기열 Active 대기 (401 자동 갱신 포함) ──
            PrintStep("Test D", "대기열 Active 대기 (401 발생 시 자동 갱신 + 재시도)");
            Console.WriteLine("  (대기 중 Access Token이 만료되면 자동으로 갱신합니다)");
            Console.WriteLine();
            var (activated, finalSession) = await WaitUntilActiveAsync(http, currentSession);

            Console.WriteLine();
            if (activated)
                Pass("대기열 통과! Active 상태 획득 완료.");
            else
                Fail("타임아웃 또는 오류로 Active 획득 실패.");

            WaitEnter();
        }

        // ── 대기열 Active 폴링 ────────────────────────────────────
        // 401 수신 시 Refresh → 재시도. 갱신된 session을 반환합니다.

        private static async Task<(bool activated, TokenSession session)> WaitUntilActiveAsync(
            HttpClient http, TokenSession session)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(300));

            try
            {
                while (!timeout.IsCancellationRequested)
                {
                    var resp = await http.GetAsync(
                        $"{Consts.TICKET_API_URL}/api/queue/status",
                        timeout.Token);

                    if (resp.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        Console.WriteLine("  [401] Access Token 만료 → Refresh 시도...");
                        var newSession = await AuthHelper.TryRefreshAsync(http, session);
                        if (newSession == null)
                        {
                            Fail("세션 만료. 재로그인이 필요합니다.");
                            return (false, session);
                        }
                        session = newSession;
                        AuthHelper.ApplyToken(http, session);
                        Console.WriteLine("  [OK] 토큰 갱신 완료. 폴링 재개...");
                        continue;
                    }

                    if (resp.StatusCode == HttpStatusCode.NotFound)
                    {
                        await Task.Delay(2000, timeout.Token);
                        continue;
                    }

                    if (!resp.IsSuccessStatusCode)
                        return (false, session);

                    var data = await resp.Content.ReadFromJsonAsync<QueueStatusDto>(
                        cancellationToken: timeout.Token);

                    if (data?.Status == "Active")
                        return (true, session);

                    Console.WriteLine($"  ⏳ 대기 중... (앞에 {data?.Rank}명 / {data?.NextPollDelay}ms 후 재확인)");
                    await Task.Delay(data?.NextPollDelay ?? 3000, timeout.Token);
                }
            }
            catch (OperationCanceledException) { }

            return (false, session);
        }

        // ── 출력 헬퍼 ─────────────────────────────────────────────

        private static void PrintHeader()
        {
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine("   [Scenario 7] 단일 유저 인증/대기열 통합 테스트");
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine("  [A] 재로그인 시 구 Refresh Token 자동 폐기");
            Console.WriteLine("  [B] Refresh Token → 새 Access/Refresh Token 발급");
            Console.WriteLine("  [C] 갱신 토큰으로 대기열 API 호출");
            Console.WriteLine("  [D] 대기열 Active 대기 중 401 자동 갱신");
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine();
        }

        private static void PrintStep(string tag, string description)
            => Console.WriteLine($"\n  ── [{tag}] {description}");

        private static void Pass(string msg)
            => Console.WriteLine($"  [PASS] {msg}");

        private static void Fail(string msg)
            => Console.WriteLine($"  [FAIL] {msg}");

        private static void WaitEnter()
        {
            Console.WriteLine("\n  엔터를 누르면 메뉴로 돌아갑니다.");
            Console.ReadLine();
        }

        private static string Head(string s) => s.Length >= 20 ? s[..20] : s;
        private static string Tail(string s) => s.Length >= 20 ? s[^20..] : s;

    }
}
