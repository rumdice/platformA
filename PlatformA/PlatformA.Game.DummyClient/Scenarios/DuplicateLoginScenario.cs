using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using PlatformA.Library.Common;
using PlatformA.Library.Packets;

namespace PlatformA.Game.DummyClient.Scenarios
{
    /// <summary>
    /// 시나리오 8: 단일 유저 중복 로그인 방어 검증
    ///
    /// 검증 항목:
    ///   [Step 1] Auth.API 로그인 → Access Token 획득
    ///   [Step 2] Ticketing.API 대기열 진입 → Active 상태까지 대기
    ///   [Step 3] 동일 JWT 토큰으로 게임 서버에 동시에 두 번 TCP 연결 시도
    ///   [Test A] 첫 번째 연결 → S_Login(ResultSuccess=0) 확인
    ///   [Test B] 두 번째 연결 → S_Login(ResultDuplicate=3) 차단 확인
    ///
    /// 검증 원리:
    ///   게임 서버는 ProcessLoginAsync 내부에서 Redis 분산 락(player:login_lock:{playerId})을
    ///   획득한 세션만 방 입장을 허용합니다. 두 연결이 동시에 도달하면 락을 먼저 획득한
    ///   쪽만 성공하고, 나머지는 ResultDuplicate를 수신한 뒤 연결이 끊어져야 합니다.
    /// </summary>
    public class DuplicateLoginScenario
    {
        public static async Task RunAsync()
        {
            try { Console.Clear(); } catch { }
            PrintHeader();

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };

            // ── [Step 1] 계정 입력 ────────────────────────────────────
            Console.Write("  아이디를 입력하세요: ");
            string username = Console.ReadLine()?.Trim() ?? "";
            Console.Write("  비밀번호를 입력하세요: ");
            string password = Console.ReadLine()?.Trim() ?? "";
            Console.WriteLine();

            PrintStep("Step 1", "Auth.API 로그인");
            var session = await AuthHelper.LoginAsync(http, username, password);
            if (session == null)
            {
                Fail("로그인 실패. 서버 상태 및 계정 정보를 확인하세요.");
                WaitEnter();
                return;
            }
            Console.WriteLine($"  [OK] PlayerId={session.PlayerId}");
            Console.WriteLine($"       AccessToken : ...{Tail(session.AccessToken)}");

            // ── [Step 2] 대기열 진입 + Active 대기 ───────────────────
            PrintStep("Step 2", "대기열 진입 → Active 상태 대기");
            AuthHelper.ApplyToken(http, session);

            var enterResp = await http.PostAsync($"{Consts.TICKET_API_URL}/api/queue/enter", null);
            if (!enterResp.IsSuccessStatusCode)
            {
                string msg = await enterResp.Content.ReadAsStringAsync();
                Fail($"대기열 진입 실패: HTTP {(int)enterResp.StatusCode} — {msg}");
                WaitEnter();
                return;
            }
            Console.WriteLine("  [OK] 대기열 진입 완료. Active 상태 대기 중...");

            var (activated, finalSession) = await WaitUntilActiveAsync(http, session);
            if (!activated)
            {
                Fail("Active 상태 획득 실패. 타임아웃 또는 서버 오류.");
                WaitEnter();
                return;
            }
            Console.WriteLine("  [OK] Active 상태 획득 완료!");

            // ── [Step 3] 동시 이중 TCP 연결 시도 ─────────────────────
            PrintStep("Step 3", "게임 서버 동시 이중 TCP 연결 시도");
            Console.WriteLine("  두 연결을 동시에 발사합니다...");
            Console.WriteLine();

            var task1 = ConnectAndLoginAsync(finalSession.AccessToken, connectionLabel: "연결 #1");
            var task2 = ConnectAndLoginAsync(finalSession.AccessToken, connectionLabel: "연결 #2");

            int[] results = await Task.WhenAll(task1, task2);
            int result1 = results[0];
            int result2 = results[1];

            // ── 결과 분석 ─────────────────────────────────────────────
            Console.WriteLine();
            Console.WriteLine("  ── 결과 분석 ──────────────────────────────────────");
            Console.WriteLine($"  연결 #1 응답 코드 : {result1} ({GetResultName(result1)})");
            Console.WriteLine($"  연결 #2 응답 코드 : {result2} ({GetResultName(result2)})");
            Console.WriteLine();

            int successCode = (int)LoginResultCode.LoginSuccess;
            int duplicateCode = (int)LoginResultCode.LoginDuplicate;

            bool oneSuccess = result1 == successCode || result2 == successCode;
            bool oneDuplicate = result1 == duplicateCode || result2 == duplicateCode;

            // [Test A] 한 연결은 성공해야 한다
            PrintStep("Test A", "정상 연결 1개 성공 확인");
            if (oneSuccess)
                Pass($"정상 접속 1개 성공 (ResultCode={successCode})");
            else
                Fail($"정상 성공 연결이 없습니다. 두 응답 코드: {result1}, {result2}");

            // [Test B] 나머지 한 연결은 중복 로그인으로 차단되어야 한다
            PrintStep("Test B", "중복 로그인 차단 확인 (ResultDuplicate=3)");
            if (oneDuplicate)
                Pass($"중복 로그인 차단 동작 확인 (ResultCode={duplicateCode})");
            else
                Fail($"중복 로그인이 차단되지 않았습니다! 두 응답 코드: {result1}, {result2}\n" +
                     "  → Redis 분산 락(player:login_lock) 설정을 확인하세요.");

            // 최종 판정
            Console.WriteLine();
            if (oneSuccess && oneDuplicate)
            {
                Console.WriteLine("  ══════════════════════════════════════════════════");
                Console.WriteLine("  [PASS] 중복 로그인 방어가 정상적으로 동작합니다.");
                Console.WriteLine("  ══════════════════════════════════════════════════");
            }
            else
            {
                Console.WriteLine("  ══════════════════════════════════════════════════");
                Console.WriteLine("  [FAIL] 중복 로그인 방어에 문제가 있습니다.");
                Console.WriteLine("  ══════════════════════════════════════════════════");
            }

            WaitEnter();
        }

        // ── 게임 서버 TCP 연결 + 로그인 응답 수신 ────────────────────

        private static async Task<int> ConnectAndLoginAsync(string jwtToken, string connectionLabel)
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(Consts.GAME_SERVER_IP, Consts.GAME_SERVER_PORT);

                byte[] sendBuf = PacketHelper.BuildPacket(new Packet { CLogin = new CLogin { RoomId = 1, JwtToken = jwtToken } });
                await socket.SendAsync(sendBuf, SocketFlags.None);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                byte[]? frame = await PacketHelper.ReceiveFrameAsync(socket, cts.Token);
                if (frame == null)
                {
                    Console.WriteLine($"  [{connectionLabel}] 응답 타임아웃 또는 연결 끊김");
                    return -1;
                }

                try
                {
                    Packet envelope = PacketHelper.ParseEnvelope(frame);
                    if (envelope.PayloadCase != Packet.PayloadOneofCase.SLogin)
                    {
                        Console.WriteLine($"  [{connectionLabel}] 예상치 못한 패킷: {envelope.PayloadCase}");
                        return -1;
                    }
                    int code = (int)envelope.SLogin.ResultCode;
                    Console.WriteLine($"  [{connectionLabel}] S_Login 수신 → ResultCode={code} ({GetResultName(code)})");
                    return code;
                }
                catch
                {
                    Console.WriteLine($"  [{connectionLabel}] S_Login 파싱 실패");
                    return -1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [{connectionLabel}] 예외 발생: {ex.GetType().Name} — {ex.Message}");
                return -1;
            }
        }

        // ── 대기열 Active 폴링 (401 자동 갱신 포함) ──────────────────

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
                            return (false, session);
                        session = newSession;
                        AuthHelper.ApplyToken(http, session);
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

        // ── 출력 헬퍼 ─────────────────────────────────────────────────

        private static string GetResultName(int code) => code switch
        {
            (int)LoginResultCode.LoginSuccess => "성공",
            (int)LoginResultCode.LoginInvalidToken => "JWT 오류",
            (int)LoginResultCode.LoginNotInQueue => "대기열 미통과",
            (int)LoginResultCode.LoginDuplicate => "중복 로그인 차단",
            (int)LoginResultCode.LoginRoomNotFound => "방 없음",
            -1 => "타임아웃/예외",
            _ => $"알 수 없음({code})"
        };

        private static void PrintHeader()
        {
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine("   [Scenario 8] 단일 유저 중복 로그인 방어 검증");
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine("  [A] 동일 JWT로 게임 서버에 동시 이중 연결 시도");
            Console.WriteLine("  [B] 한 연결 성공, 다른 연결 ResultDuplicate(3) 차단 확인");
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

        private static string Tail(string s) => s.Length >= 20 ? s[^20..] : s;
    }
}
