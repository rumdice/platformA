using PlatformA.Library.Common;
using PlatformA.Library.Packets;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace PlatformA.Game.DummyClient.Scenarios
{
    /// <summary>
    /// 시나리오 1: 1000명 로그인 + 대기열 통과 부하 테스트
    ///
    /// 흐름:
    ///   1. Auth.API 로그인 (계정 없으면 자동 생성, 패스워드 "1234")
    ///   2. Ticketing.API 대기열 진입
    ///   3. 스마트 폴링으로 Active 상태 대기
    ///
    /// 설정:
    ///   - 유저 수       : USER_COUNT (기본 1000)
    ///   - 스폰 속도     : SPAWN_RATE_PER_SEC (기본 50명/초)
    ///   - 대기열 처리   : QueueWorkerService가 서버에서 50명/초 처리
    /// </summary>
    public class LoginWaitScenario_1
    {
        // ── 시나리오 파라미터 ─────────────────────────────────────
        private const int    USER_COUNT          = 1000;
        private const int    SPAWN_RATE_PER_SEC  = 50;
        private const int    SPAWN_INTERVAL_MS   = 1000 / SPAWN_RATE_PER_SEC; // 20ms
        private const string USERNAME_PREFIX     = "lt_";   // lt_0001 ~ lt_1000
        private const string PASSWORD            = "123456";
        private const int    HTTP_TIMEOUT_SEC    = 120;
        // ─────────────────────────────────────────────────────────

        // ── 측정 지표 (Interlocked — 멀티스레드 안전) ─────────────
        private static int  _loginOk;
        private static int  _loginFail;
        private static int  _loginRateLimit;     // HTTP 429
        private static int  _queueOk;
        private static int  _queueFail;
        private static int  _activeOk;
        private static int  _activeFail;
        private static int  _gameLoginOk;        // TCP 게임서버 로그인 성공
        private static int  _gameLoginFail;      // TCP 게임서버 로그인 실패
        private static int  _completed;
        private static long _totalWaitMs;        // Active 획득까지 총 대기 시간(ms)
        // ─────────────────────────────────────────────────────────

        public static async Task RunAsync()
        {
            Console.Clear();
            PrintHeader();

            ResetCounters();
            var sw = Stopwatch.StartNew();

            // 진행 상황 라이브 표시 (2초 주기)
            using var displayCts = new CancellationTokenSource();
            var displayTask = LiveProgressAsync(displayCts.Token);

            // 유저 태스크 생성 (SPAWN_RATE_PER_SEC명/초로 분산 투입)
            var tasks = new List<Task>(USER_COUNT);
            for (int i = 1; i <= USER_COUNT; i++)
            {
                int userId = i;
                tasks.Add(SimulateUserAsync(userId));
                await Task.Delay(SPAWN_INTERVAL_MS);
            }

            // 모든 유저 완료 대기
            await Task.WhenAll(tasks);
            sw.Stop();

            // 진행 표시 종료
            displayCts.Cancel();
            try { await displayTask; } catch (OperationCanceledException) { }

            PrintFinalReport(sw.Elapsed);
        }

        // ── 개별 유저 시나리오 ────────────────────────────────────

        private static async Task SimulateUserAsync(int userId)
        {
            // 유저별 가상 IP → 각자 독립적인 Rate Limit 버킷 사용
            string fakeIp = $"10.{userId / 256}.{userId % 256}.1";

            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(HTTP_TIMEOUT_SEC);
            http.DefaultRequestHeaders.Add("X-Forwarded-For", fakeIp);

            var userSw = Stopwatch.StartNew();

            // ── STEP 1: 로그인 ────────────────────────────────────
            string username = $"{USERNAME_PREFIX}{userId:D4}";
            string? token = await LoginAsync(http, username);
            if (token == null) { Interlocked.Increment(ref _completed); return; }

            // ── STEP 2: 대기열 진입 ───────────────────────────────
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            bool entered = await EnterQueueAsync(http, userId);
            if (!entered) { Interlocked.Increment(ref _completed); return; }

            // ── STEP 3: Active 상태 폴링 ──────────────────────────
            bool activated = await PollUntilActiveAsync(http, userId);

            if (activated)
            {
                Interlocked.Increment(ref _activeOk);
                Interlocked.Add(ref _totalWaitMs, userSw.ElapsedMilliseconds);

                // ── STEP 4: 게임 서버 TCP 로그인 ──────────────────
                bool gameLoginOk = await ConnectToGameServerAsync(token);
                if (gameLoginOk)
                    Interlocked.Increment(ref _gameLoginOk);
                else
                    Interlocked.Increment(ref _gameLoginFail);
            }
            else
            {
                Interlocked.Increment(ref _activeFail);
            }

            Interlocked.Increment(ref _completed);
        }

        // ── STEP 1: Auth.API 로그인 ───────────────────────────────

        private static async Task<string?> LoginAsync(HttpClient http, string username)
        {
            try
            {
                var body = new { Username = username, Password = PASSWORD };
                var resp = await http.PostAsJsonAsync(Consts.AUTH_API_URL, body);

                if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    Interlocked.Increment(ref _loginRateLimit);
                    return null;
                }
                if (!resp.IsSuccessStatusCode)
                {
                    int n = Interlocked.Increment(ref _loginFail);
                    if (n <= 3)
                    {
                        string body2 = await resp.Content.ReadAsStringAsync();
                        Console.WriteLine($"  [LoginFail] {username} → HTTP {(int)resp.StatusCode}: {body2[..Math.Min(120, body2.Length)]}");
                    }
                    return null;
                }

                string json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                string token = doc.RootElement.GetProperty("token").GetString()!;
                Interlocked.Increment(ref _loginOk);
                return token;
            }
            catch (Exception ex)
            {
                int n = Interlocked.Increment(ref _loginFail);
                if (n <= 3)
                    Console.WriteLine($"  [LoginFail] {username} → {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        // ── STEP 2: 대기열 진입 ───────────────────────────────────

        private static async Task<bool> EnterQueueAsync(HttpClient http, int userId)
        {
            try
            {
                var resp = await http.PostAsync($"{Consts.TICKET_API_URL}/api/queue/enter", null);
                if (resp.IsSuccessStatusCode)
                {
                    Interlocked.Increment(ref _queueOk);
                    return true;
                }

                Interlocked.Increment(ref _queueFail);
                return false;
            }
            catch
            {
                Interlocked.Increment(ref _queueFail);
                return false;
            }
        }

        // ── STEP 3: Active 폴링 ───────────────────────────────────

        private static async Task<bool> PollUntilActiveAsync(HttpClient http, int userId)
        {
            // 최대 대기 시간: 대기열이 모두 소진될 때까지 여유 있게 설정
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(300));

            try
            {
                while (!timeout.IsCancellationRequested)
                {
                    var resp = await http.GetAsync(
                        $"{Consts.TICKET_API_URL}/api/queue/status",
                        timeout.Token);

                    // 404: 워커가 큐에서 꺼낸 직후 Active 키 미설정 타이밍 → 재시도
                    if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        await Task.Delay(2000, timeout.Token);
                        continue;
                    }
                    if (!resp.IsSuccessStatusCode) return false;

                    var data = await resp.Content.ReadFromJsonAsync<QueueStatusDto>(
                        cancellationToken: timeout.Token);

                    if (data?.Status == "Active") return true;

                    int delay = data?.NextPollDelay ?? 3000;
                    await Task.Delay(delay, timeout.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch { /* 연결 오류 */ }

            return false;
        }

        // ── 진행 상황 실시간 출력 ─────────────────────────────────

        private static async Task LiveProgressAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(2000, ct);
                    int done        = _completed;
                    int queueActive = Math.Max(0, _queueOk - _activeOk - _activeFail);
                    Console.WriteLine($"[진행] 완료 {done,4} / {USER_COUNT}");
                    Console.WriteLine(
                        $"Auth 로그인 : {_loginOk} 성공  {_loginFail + _loginRateLimit} 실패 │" +
                        $"대기열 : {queueActive} 대기중 │" +
                        $"입장 : {_activeOk} 성공  {_activeFail} 실패 │" +
                        $"게임서버 로그인 : {_gameLoginOk} 성공  {_gameLoginFail} 실패");
                }
            }
            catch (OperationCanceledException) { }
        }

        // ── 최종 리포트 ───────────────────────────────────────────

        private static void PrintFinalReport(TimeSpan elapsed)
        {
            Console.WriteLine();
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine("  📊 최종 결과 리포트");
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine($"  총 소요 시간      : {elapsed.TotalSeconds:F1}초");
            Console.WriteLine($"  스폰 속도         : {SPAWN_RATE_PER_SEC}명/초  ({USER_COUNT}명 스폰 완료)");
            Console.WriteLine();
            Console.WriteLine($"  [Auth.API 로그인]");
            Console.WriteLine($"    성공            : {_loginOk,5}명");
            Console.WriteLine($"    실패 (서버 오류) : {_loginFail,5}명");
            Console.WriteLine($"    Rate Limit(429)  : {_loginRateLimit,5}명");
            Console.WriteLine();
            Console.WriteLine($"  [Ticketing.API 대기열]");
            Console.WriteLine($"    진입 성공       : {_queueOk,5}명");
            Console.WriteLine($"    진입 실패       : {_queueFail,5}명");
            Console.WriteLine();
            Console.WriteLine($"  [입장권(Active) 획득]");
            Console.WriteLine($"    획득 성공       : {_activeOk,5}명");
            Console.WriteLine($"    획득 실패/타임아웃: {_activeFail,5}명");
            Console.WriteLine();
            Console.WriteLine($"  [게임 서버 TCP 로그인]");
            Console.WriteLine($"    로그인 성공     : {_gameLoginOk,5}명");
            Console.WriteLine($"    로그인 실패     : {_gameLoginFail,5}명");

            if (_activeOk > 0)
            {
                double avgWaitSec = (_totalWaitMs / (double)_activeOk) / 1000.0;
                double throughput = _activeOk / elapsed.TotalSeconds;
                Console.WriteLine();
                Console.WriteLine($"  [성능 지표]");
                Console.WriteLine($"    평균 전체 대기   : {avgWaitSec:F1}초/유저");
                Console.WriteLine($"    실질 처리량      : {throughput:F1}명/초");
            }

            Console.WriteLine();
            PrintBottleneckAnalysis();
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine("  엔터를 누르면 메뉴로 돌아갑니다.");
            Console.ReadLine();
        }

        private static void PrintBottleneckAnalysis()
        {
            Console.WriteLine($"  [병목 분석]");

            if (_loginRateLimit > 0)
                Console.WriteLine($"    ⚠️  Rate Limit 초과 {_loginRateLimit}건: X-Forwarded-For 헤더가 전달되지 않거나 IP별 버킷 설정 확인 필요");

            if (_loginFail > 10)
                Console.WriteLine($"    ⚠️  로그인 실패 {_loginFail}건: Auth.API BCrypt 연산 부하 또는 DB 연결 확인 필요");

            if (_queueFail > 0)
                Console.WriteLine($"    ⚠️  대기열 진입 실패 {_queueFail}건: Ticketing.API 또는 Redis 상태 확인 필요");

            if (_activeFail > 10)
                Console.WriteLine($"    ⚠️  Active 획득 실패 {_activeFail}건: QueueWorkerService 처리 속도 또는 Ghost 정리 로직 확인 필요");

            if (_gameLoginFail > 0)
                Console.WriteLine($"    ⚠️  게임서버 로그인 실패 {_gameLoginFail}건: Game.Server 상태 또는 Active 키 타이밍 확인 필요");

            int successRate = USER_COUNT > 0 ? (_gameLoginOk * 100 / USER_COUNT) : 0;
            if (successRate >= 95)
                Console.WriteLine($"    ✅  성공률 {successRate}% — 안정적. 유저 수 증가 테스트 권장 (2000명, 5000명)");
            else if (successRate >= 80)
                Console.WriteLine($"    🔶  성공률 {successRate}% — 일부 실패. 서비스 튜닝 후 재테스트 권장");
            else
                Console.WriteLine($"    ❌  성공률 {successRate}% — 병목 해소 필요. 로그 확인 요망");
        }

        private static void PrintHeader()
        {
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine("   🚦 LoginWait Scenario 1 — 1000명 대기열 부하 테스트");
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine($"  유저 수: {USER_COUNT}명  │  스폰 속도: {SPAWN_RATE_PER_SEC}명/초");
            Console.WriteLine($"  계정명: {USERNAME_PREFIX}0001 ~ {USERNAME_PREFIX}{USER_COUNT:D4}  │  대기열 처리: 50명/초");
            Console.WriteLine();
            Console.WriteLine("  ※ 서버 실행 여부 확인:");
            Console.WriteLine($"    - Auth.API      : {Consts.AUTH_API_URL}");
            Console.WriteLine($"    - Ticketing.API : {Consts.TICKET_API_URL}/api/queue/enter");
            Console.WriteLine("  ※ DB는 CF 스크립트로 미리 생성되어 있어야 합니다.");
            Console.WriteLine();
            Console.WriteLine("  테스트 시작 중...");
            Console.WriteLine("──────────────────────────────────────────────────────");
        }

        // ── STEP 4: 게임 서버 TCP 로그인 (부하 테스트용, 비대화형) ──

        private static async Task<bool> ConnectToGameServerAsync(string token)
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(Consts.GAME_SERVER_IP, Consts.GAME_SERVER_PORT);

                // C_Login 패킷 조립 (roomId=1 광장)
                byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
                ushort stringLen  = (ushort)tokenBytes.Length;
                ushort packetSize = (ushort)(4 + 4 + 2 + stringLen);
                byte[] sendBuf    = new byte[packetSize];
                Span<byte> span   = sendBuf.AsSpan();
                BitConverter.TryWriteBytes(span.Slice(0, 2), packetSize);
                BitConverter.TryWriteBytes(span.Slice(2, 2), (ushort)PacketID.C_Login);
                BitConverter.TryWriteBytes(span.Slice(4, 4), 1); // roomId = 1
                BitConverter.TryWriteBytes(span.Slice(8, 2), stringLen);
                tokenBytes.CopyTo(span.Slice(10));
                await socket.SendAsync(sendBuf, SocketFlags.None);

                // S_Login 수신 대기 (최대 10초)
                byte[] recvBuf  = new byte[64];
                var recvTask    = socket.ReceiveAsync(recvBuf, SocketFlags.None);
                if (await Task.WhenAny(recvTask, Task.Delay(10_000)) != recvTask)
                    return false; // 타임아웃

                int received = await recvTask;
                if (received < 12) return false; // header(4) + body(8)

                ushort respId = BitConverter.ToUInt16(recvBuf, 2);
                if (respId != (ushort)PacketID.S_Login) return false;

                int resultCode = BitConverter.ToInt32(recvBuf, 4);
                return resultCode == S_LoginPacket.ResultSuccess;
            }
            catch { return false; }
        }

        private static void ResetCounters()
        {
            _loginOk = _loginFail = _loginRateLimit = 0;
            _queueOk = _queueFail = 0;
            _activeOk = _activeFail = 0;
            _gameLoginOk = _gameLoginFail = 0;
            _completed = 0;
            _totalWaitMs = 0;
        }

        // ── 응답 DTO ─────────────────────────────────────────────
        private class QueueStatusDto
        {
            public int    UserId        { get; set; }
            public long   Rank          { get; set; }
            public string Status        { get; set; } = "";
            public int    NextPollDelay { get; set; }
        }
    }
}
