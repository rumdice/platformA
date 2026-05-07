using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.SignalR.Client;
using PlatformA.Library.Common;
using PlatformA.Library.Packets;

namespace PlatformA.Game.DummyClient.Scenarios
{
    /// <summary>
    /// 시나리오 4: 1000명 로그인 + 대기열 통과 부하 테스트
    ///
    /// 흐름:
    ///   1. Auth.API 로그인 → TokenSession (AccessToken + RefreshToken) 획득
    ///   2. Ticketing.API 대기열 진입
    ///   3. SignalR push + fallback 폴링으로 Active 상태 대기
    ///      └─ 대기 중 401 수신 시 Refresh Token으로 자동 갱신 후 재시도
    ///   4. 게임 서버 TCP 로그인
    ///
    /// 설정:
    ///   - 유저 수       : USER_COUNT (기본 1000)
    ///   - 스폰 속도     : SPAWN_RATE_PER_SEC (기본 50명/초)
    ///   - 대기열 처리   : QueueWorkerService가 서버에서 50명/초 처리
    /// </summary>
    public class LoginWaitScenario_1
    {
        // ── 시나리오 파라미터 ─────────────────────────────────────
        private const int USER_COUNT = 1000;
        private const int SPAWN_RATE_PER_SEC = 50;
        private const int SPAWN_INTERVAL_MS = 1000 / SPAWN_RATE_PER_SEC; // 20ms
        private const string USERNAME_PREFIX = "lt_";   // lt_0001 ~ lt_1000
        private const string PASSWORD = "123456";
        private const int HTTP_TIMEOUT_SEC = 120;
        // ─────────────────────────────────────────────────────────

        // ── 측정 지표 (Interlocked — 멀티스레드 안전) ─────────────
        private static int _loginOk;
        private static int _loginFail;
        private static int _loginRateLimit;     // HTTP 429
        private static int _queueOk;
        private static int _queueFail;
        private static int _activeOk;
        private static int _activeFail;
        private static int _tokenRefreshCount;  // 대기 중 Access Token 갱신 횟수
        private static int _gameLoginOk;
        private static int _gameLoginFail;
        private static int _completed;
        private static long _totalWaitMs;
        // ─────────────────────────────────────────────────────────

        public static async Task RunAsync()
        {
            Console.Clear();
            PrintHeader();

            ResetCounters();
            var sw = Stopwatch.StartNew();

            using var displayCts = new CancellationTokenSource();
            var displayTask = LiveProgressAsync(displayCts.Token);

            var tasks = new List<Task>(USER_COUNT);
            for (int i = 1; i <= USER_COUNT; i++)
            {
                int userId = i;
                tasks.Add(SimulateUserAsync(userId));
                await Task.Delay(SPAWN_INTERVAL_MS);
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            displayCts.Cancel();
            try
            { await displayTask; }
            catch (OperationCanceledException) { }

            PrintFinalReport(sw.Elapsed);
        }

        // ── 개별 유저 시나리오 ────────────────────────────────────

        private static async Task SimulateUserAsync(int userId)
        {
            string fakeIp = $"10.{userId / 256}.{userId % 256}.1";

            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(HTTP_TIMEOUT_SEC);
            http.DefaultRequestHeaders.Add("X-Forwarded-For", fakeIp);

            var userSw = Stopwatch.StartNew();

            // ── STEP 1: 로그인 ────────────────────────────────────
            string username = $"{USERNAME_PREFIX}{userId:D4}";
            var session = await LoginAsync(http, username);
            if (session == null)
            { Interlocked.Increment(ref _completed); return; }

            // ── STEP 2: 대기열 진입 ───────────────────────────────
            AuthHelper.ApplyToken(http, session);
            bool entered = await EnterQueueAsync(http, userId);
            if (!entered)
            { Interlocked.Increment(ref _completed); return; }

            // ── STEP 3: Active 상태 대기 ──────────────────────────
            var (activated, finalSession) = await WaitUntilActiveAsync(http, session);

            if (activated)
            {
                Interlocked.Increment(ref _activeOk);
                Interlocked.Add(ref _totalWaitMs, userSw.ElapsedMilliseconds);

                // ── STEP 4: 게임 서버 TCP 로그인 ──────────────────
                bool gameLoginOk = await ConnectToGameServerAsync(finalSession.AccessToken);
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

        private static async Task<TokenSession?> LoginAsync(HttpClient http, string username)
        {
            try
            {
                var session = await AuthHelper.LoginAsync(http, username, PASSWORD);
                if (session != null)
                {
                    Interlocked.Increment(ref _loginOk);
                    return session;
                }

                // 429 여부는 직접 확인이 필요하므로 fallback으로 statusCode 조회
                // (AuthHelper는 단순 null 반환이므로 여기서는 loginFail로 집계)
                int n = Interlocked.Increment(ref _loginFail);
                if (n <= 3)
                    Console.WriteLine($"  [LoginFail] {username}");
                return null;
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

        // ── STEP 3: Active 대기 (SignalR push + fallback 폴링) ────
        // 대기 중 401 수신 시 Refresh Token으로 자동 갱신 후 재시도합니다.
        // 갱신된 session을 반환하여 이후 게임 서버 로그인에 최신 토큰이 사용됩니다.

        private static async Task<(bool activated, TokenSession session)> WaitUntilActiveAsync(
            HttpClient http, TokenSession session)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(300));

            var hub = new HubConnectionBuilder()
                .WithUrl($"{Consts.TICKET_API_URL}/hubs/queue", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(session.AccessToken);
                    options.HttpMessageHandlerFactory = _ =>
                        new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
                })
                .Build();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            hub.On("QueueActivated", () => tcs.TrySetResult(true));

            bool signalRConnected = false;
            try
            {
                await hub.StartAsync(timeout.Token);
                signalRConnected = true;
            }
            catch { /* SignalR 연결 실패 → fallback 폴링으로 진행 */ }

            try
            {
                while (!timeout.IsCancellationRequested)
                {
                    if (signalRConnected)
                    {
                        var pollDelay = Task.Delay(10_000, timeout.Token);
                        var completed = await Task.WhenAny(tcs.Task, pollDelay);
                        if (completed == tcs.Task)
                            return (true, session);
                    }

                    var resp = await http.GetAsync(
                        $"{Consts.TICKET_API_URL}/api/queue/status",
                        timeout.Token);

                    // 401: Access Token 만료 → Refresh Token으로 갱신
                    if (resp.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        var newSession = await AuthHelper.TryRefreshAsync(http, session);
                        if (newSession == null)
                            return (false, session); // 세션 완전 만료
                        session = newSession;
                        AuthHelper.ApplyToken(http, session);
                        Interlocked.Increment(ref _tokenRefreshCount);
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

                    if (!signalRConnected)
                    {
                        int delay = data?.NextPollDelay ?? 3000;
                        await Task.Delay(delay, timeout.Token);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch { /* 연결 오류 */ }
            finally
            {
                await hub.DisposeAsync();
            }

            return (false, session);
        }

        // ── 진행 상황 실시간 출력 ─────────────────────────────────

        private static async Task LiveProgressAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(2000, ct);
                    int done = _completed;
                    int queueActive = Math.Max(0, _queueOk - _activeOk - _activeFail);
                    Console.WriteLine($"[진행] 완료 {done,4} / {USER_COUNT}");
                    Console.WriteLine(
                        $"Auth 로그인 : {_loginOk} 성공  {_loginFail + _loginRateLimit} 실패 │" +
                        $"대기열 : {queueActive} 대기중 │" +
                        $"입장 : {_activeOk} 성공  {_activeFail} 실패 │" +
                        $"토큰갱신 : {_tokenRefreshCount}회 │" +
                        $"게임서버 : {_gameLoginOk} 성공  {_gameLoginFail} 실패");
                }
            }
            catch (OperationCanceledException) { }
        }

        // ── 최종 리포트 ───────────────────────────────────────────

        private static void PrintFinalReport(TimeSpan elapsed)
        {
            Console.WriteLine();
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine("  최종 결과 리포트");
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
            Console.WriteLine($"    대기 중 토큰 갱신 : {_tokenRefreshCount,5}회");
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
                Console.WriteLine($"    ⚠  Rate Limit 초과 {_loginRateLimit}건: X-Forwarded-For 헤더 또는 IP별 버킷 설정 확인 필요");

            if (_loginFail > 10)
                Console.WriteLine($"    ⚠  로그인 실패 {_loginFail}건: Auth.API BCrypt 연산 부하 또는 DB 연결 확인 필요");

            if (_queueFail > 0)
                Console.WriteLine($"    ⚠  대기열 진입 실패 {_queueFail}건: Ticketing.API 또는 Redis 상태 확인 필요");

            if (_activeFail > 10)
                Console.WriteLine($"    ⚠  Active 획득 실패 {_activeFail}건: QueueWorkerService 처리 속도 또는 Ghost 정리 로직 확인 필요");

            if (_tokenRefreshCount > 0)
                Console.WriteLine($"    ⚠  토큰 갱신 {_tokenRefreshCount}회: 대기 시간이 15분을 초과한 유저 존재 — 대기열 처리 속도 확인 필요");

            if (_gameLoginFail > 0)
                Console.WriteLine($"    ⚠  게임서버 로그인 실패 {_gameLoginFail}건: Game.Server 상태 또는 Active 키 타이밍 확인 필요");

            int successRate = USER_COUNT > 0 ? (_gameLoginOk * 100 / USER_COUNT) : 0;
            if (successRate >= 95)
                Console.WriteLine($"    OK  성공률 {successRate}% — 안정적. 유저 수 증가 테스트 권장 (2000명, 5000명)");
            else if (successRate >= 80)
                Console.WriteLine($"    --  성공률 {successRate}% — 일부 실패. 서비스 튜닝 후 재테스트 권장");
            else
                Console.WriteLine($"    NG  성공률 {successRate}% — 병목 해소 필요. 로그 확인 요망");
        }

        private static void PrintHeader()
        {
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine("   [Scenario 4] 1000명 대기열 부하 테스트");
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine($"  유저 수: {USER_COUNT}명  │  스폰 속도: {SPAWN_RATE_PER_SEC}명/초");
            Console.WriteLine($"  계정명: {USERNAME_PREFIX}0001 ~ {USERNAME_PREFIX}{USER_COUNT:D4}  │  대기열 처리: 50명/초");
            Console.WriteLine();
            Console.WriteLine("  서버 실행 여부 확인:");
            Console.WriteLine($"    - Auth.API      : {Consts.AUTH_API_URL}");
            Console.WriteLine($"    - Ticketing.API : {Consts.TICKET_API_URL}/api/queue/enter");
            Console.WriteLine("  DB는 CF 스크립트로 미리 생성되어 있어야 합니다.");
            Console.WriteLine();
            Console.WriteLine("  테스트 시작 중...");
            Console.WriteLine("──────────────────────────────────────────────────────");
        }

        // ── STEP 4: 게임 서버 TCP 로그인 (부하 테스트용) ─────────

        private static async Task<bool> ConnectToGameServerAsync(string token)
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(Consts.GAME_SERVER_IP, Consts.GAME_SERVER_PORT);

                byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
                ushort stringLen = (ushort)tokenBytes.Length;
                ushort packetSize = (ushort)(4 + 4 + 2 + stringLen);
                byte[] sendBuf = new byte[packetSize];
                Span<byte> span = sendBuf.AsSpan();
                BitConverter.TryWriteBytes(span.Slice(0, 2), packetSize);
                BitConverter.TryWriteBytes(span.Slice(2, 2), (ushort)PacketID.C_Login);
                BitConverter.TryWriteBytes(span.Slice(4, 4), 1); // roomId = 1
                BitConverter.TryWriteBytes(span.Slice(8, 2), stringLen);
                tokenBytes.CopyTo(span.Slice(10));
                await socket.SendAsync(sendBuf, SocketFlags.None);

                byte[] recvBuf = new byte[64];
                var recvTask = socket.ReceiveAsync(recvBuf, SocketFlags.None);
                if (await Task.WhenAny(recvTask, Task.Delay(10_000)) != recvTask)
                    return false;

                int received = await recvTask;
                if (received < 12)
                    return false;

                ushort respId = BitConverter.ToUInt16(recvBuf, 2);
                if (respId != (ushort)PacketID.S_Login)
                    return false;

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
            _tokenRefreshCount = 0;
            _gameLoginOk = _gameLoginFail = 0;
            _completed = 0;
            _totalWaitMs = 0;
        }

    }
}
