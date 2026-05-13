using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.AspNetCore.SignalR.Client;
using PlatformA.Library.Common;
using PlatformA.Library.Packets;

namespace PlatformA.Game.DummyClient.Scenarios
{
    /// <summary>
    /// 시나리오 5: 1000명 로그인 + 대기열 통과 + 매칭 시스템 부하 테스트
    ///
    /// 흐름 (유저 1명 기준):
    ///   1. Auth.API 로그인
    ///   2. Ticketing.API 대기열 진입 → Active 대기
    ///   3. Matching.API SignalR 연결 → MatchFound / MatchTimeout 핸들러 등록
    ///   4. POST /api/GameMatch/RequestMatch → 매칭 큐 등록
    ///   5. MatchFound 수신 → 매칭 지연 시간 기록
    ///   6. Game.Server TCP → CEnterRoom(roomId) → 방 입장 확인
    ///
    /// 1000명 동시 실행 → 500쌍 매칭 기대.
    /// 로그인/큐 실패 등으로 홀수 생존자 발생 시 120초 후 MatchTimeout.
    /// </summary>
    public class LoadTestMatchingScenario
    {
        // ── 시나리오 파라미터 ────────────────────────────────────────
        private const int USER_COUNT = 1000;
        private const int SPAWN_RATE_PER_SEC = 50;
        private const int SPAWN_INTERVAL_MS = 1000 / SPAWN_RATE_PER_SEC; // 20ms
        private const string USERNAME_PREFIX = "lt_";
        private const string PASSWORD = "123456";
        private const int HTTP_TIMEOUT_SEC = 120;
        private const int MATCH_WAIT_SEC = 130; // MATCH_TIMEOUT_SECONDS(120) + 여유 10초

        // ── 측정 지표 ────────────────────────────────────────────────
        private static int _loginOk, _loginFail;
        private static int _queueOk, _queueFail;
        private static int _activeOk, _activeFail;
        private static int _tokenRefreshCount;
        private static int _matchOk, _matchTimeout, _matchFail;
        private static int _roomOk, _roomFail;
        private static int _completed;

        private static readonly ConcurrentBag<long> _matchLatenciesMs = new();

        // ────────────────────────────────────────────────────────────

        public static async Task RunAsync()
        {
            try
            { Console.Clear(); }
            catch { }
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

        // ── 개별 유저 시나리오 ────────────────────────────────────────

        private static async Task SimulateUserAsync(int userId)
        {
            string fakeIp = $"10.{userId / 256}.{userId % 256}.1";
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(HTTP_TIMEOUT_SEC);
            http.DefaultRequestHeaders.Add("X-Forwarded-For", fakeIp);

            // [1] 로그인
            string username = $"{USERNAME_PREFIX}{userId:D4}";
            var session = await AuthHelper.LoginAsync(http, username, PASSWORD);
            if (session == null)
            {
                Interlocked.Increment(ref _loginFail);
                Interlocked.Increment(ref _completed);
                return;
            }
            Interlocked.Increment(ref _loginOk);
            AuthHelper.ApplyToken(http, session);

            // [2] 대기열 진입
            bool entered = await EnterQueueAsync(http);
            if (!entered)
            {
                Interlocked.Increment(ref _completed);
                return;
            }

            // [3] Active 대기
            var (activated, updatedSession, refreshes) =
                await AuthHelper.WaitUntilActiveAsync(http, session);
            Interlocked.Add(ref _tokenRefreshCount, refreshes);

            if (!activated)
            {
                Interlocked.Increment(ref _activeFail);
                Interlocked.Increment(ref _completed);
                return;
            }
            Interlocked.Increment(ref _activeOk);
            session = updatedSession;
            AuthHelper.ApplyToken(http, session);

            // [4~5] 매칭 등록 + MatchFound 대기
            int? matchedRoomId = await RequestAndWaitMatchAsync(http, session);

            if (matchedRoomId == null)
            {
                Interlocked.Increment(ref _completed);
                return;
            }

            // [6] Game.Server TCP 방 입장
            bool roomOk = await EnterRoomAsync(session.AccessToken, matchedRoomId.Value);
            if (roomOk)
                Interlocked.Increment(ref _roomOk);
            else
                Interlocked.Increment(ref _roomFail);

            Interlocked.Increment(ref _completed);
        }

        // ── STEP 2: 대기열 진입 ────────────────────────────────────────

        private static async Task<bool> EnterQueueAsync(HttpClient http)
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

        // ── STEP 4~5: 매칭 등록 + MatchFound 대기 ────────────────────

        private static async Task<int?> RequestAndWaitMatchAsync(HttpClient http, TokenSession session)
        {
            var tcs = new TaskCompletionSource<MatchSuccessEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            bool timedOut = false;

            var hub = new HubConnectionBuilder()
                .WithUrl(Consts.MATCH_HUB_URL, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(session.AccessToken);
                })
                .Build();

            hub.On<MatchSuccessEvent>("MatchFound", e => tcs.TrySetResult(e));
            hub.On<object>("MatchTimeout", _ =>
            {
                timedOut = true;
                tcs.TrySetCanceled();
            });

            try
            {
                await hub.StartAsync();
            }
            catch
            {
                Interlocked.Increment(ref _matchFail);
                await hub.DisposeAsync();
                return null;
            }

            try
            {
                await http.PostAsync(Consts.MATCH_API_URL, null);

                var sw = Stopwatch.StartNew();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(MATCH_WAIT_SEC));
                var matchEvent = await tcs.Task.WaitAsync(cts.Token);
                sw.Stop();

                _matchLatenciesMs.Add(sw.ElapsedMilliseconds);
                Interlocked.Increment(ref _matchOk);
                return matchEvent.RoomId;
            }
            catch (OperationCanceledException) when (timedOut)
            {
                Interlocked.Increment(ref _matchTimeout);
                return null;
            }
            catch
            {
                Interlocked.Increment(ref _matchFail);
                return null;
            }
            finally
            {
                await hub.DisposeAsync();
            }
        }

        // ── STEP 6: Game.Server TCP 방 입장 ──────────────────────────

        private static async Task<bool> EnterRoomAsync(string token, int roomId)
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(Consts.GAME_SERVER_IP, Consts.GAME_SERVER_PORT);

                // 로비(1번방) 로그인
                byte[] loginBuf = PacketHelper.BuildPacket(
                    new Packet { CLogin = new CLogin { RoomId = 1, JwtToken = token } });
                await socket.SendAsync(loginBuf, SocketFlags.None);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                byte[]? loginFrame = await PacketHelper.ReceiveFrameAsync(socket, cts.Token);
                if (loginFrame == null)
                    return false;

                Packet loginEnv = PacketHelper.ParseEnvelope(loginFrame);
                if (loginEnv.PayloadCase != Packet.PayloadOneofCase.SLogin ||
                    loginEnv.SLogin.ResultCode != LoginResultCode.LoginSuccess)
                    return false;

                // 매칭 방 이동
                byte[] enterBuf = PacketHelper.BuildPacket(
                    new Packet { CEnterRoom = new CEnterRoom { RoomId = roomId } });
                await socket.SendAsync(enterBuf, SocketFlags.None);

                byte[]? enterFrame = await PacketHelper.ReceiveFrameAsync(socket, cts.Token);
                if (enterFrame == null)
                    return false;

                Packet enterEnv = PacketHelper.ParseEnvelope(enterFrame);
                return enterEnv.PayloadCase == Packet.PayloadOneofCase.SEnterRoom
                    && enterEnv.SEnterRoom.ResultCode == EnterRoomResultCode.EnterRoomSuccess;
            }
            catch { return false; }
        }

        // ── 실시간 진행 출력 ──────────────────────────────────────────

        private static async Task LiveProgressAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(2000, ct);
                    Console.WriteLine(
                        $"[진행] 완료 {_completed,4}/{USER_COUNT} │" +
                        $" 로그인 {_loginOk} │ 대기열 {_queueOk} │ 입장 {_activeOk} │" +
                        $" 매칭 성사 {_matchOk} │ 타임아웃 {_matchTimeout} │ 방입장 {_roomOk}");
                }
            }
            catch (OperationCanceledException) { }
        }

        // ── 최종 리포트 ───────────────────────────────────────────────

        private static void PrintFinalReport(TimeSpan elapsed)
        {
            var latencies = _matchLatenciesMs.OrderBy(x => x).ToList();
            Console.WriteLine();
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine("  [Scenario 5] 최종 결과 리포트");
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine($"  총 소요 시간      : {elapsed.TotalSeconds:F1}초");
            Console.WriteLine();
            Console.WriteLine($"  [Auth.API 로그인]");
            Console.WriteLine($"    성공            : {_loginOk,5}명");
            Console.WriteLine($"    실패            : {_loginFail,5}명");
            Console.WriteLine();
            Console.WriteLine($"  [Ticketing.API 대기열]");
            Console.WriteLine($"    진입 성공       : {_queueOk,5}명");
            Console.WriteLine($"    진입 실패       : {_queueFail,5}명");
            Console.WriteLine($"    입장권 획득     : {_activeOk,5}명");
            Console.WriteLine($"    획득 실패/타임아웃: {_activeFail,5}명");
            Console.WriteLine($"    토큰 갱신       : {_tokenRefreshCount,5}회");
            Console.WriteLine();
            Console.WriteLine($"  [Matching.API 매칭]");

            int expectedPairs = _activeOk / 2;
            double matchRate = _activeOk > 0 ? (_matchOk * 100.0 / _activeOk) : 0;
            Console.WriteLine($"    기대 쌍 수      : {expectedPairs,5}쌍");
            Console.WriteLine($"    매칭 성사       : {_matchOk,5}명  ({matchRate:F1}%)");
            Console.WriteLine($"    타임아웃        : {_matchTimeout,5}명");
            Console.WriteLine($"    실패            : {_matchFail,5}명");

            if (latencies.Count > 0)
            {
                double matchThroughput = (_matchOk / 2.0) / elapsed.TotalSeconds;
                Console.WriteLine();
                Console.WriteLine($"  [매칭 지연 시간]");
                Console.WriteLine($"    Avg             : {latencies.Average():F0}ms");
                Console.WriteLine($"    P50             : {Percentile(latencies, 50),5}ms");
                Console.WriteLine($"    P95             : {Percentile(latencies, 95),5}ms");
                Console.WriteLine($"    P99             : {Percentile(latencies, 99),5}ms");
                Console.WriteLine($"    처리량          : {matchThroughput:F1}쌍/초");
            }

            Console.WriteLine();
            Console.WriteLine($"  [Game.Server 방 입장]");
            Console.WriteLine($"    입장 성공       : {_roomOk,5}명");
            Console.WriteLine($"    입장 실패       : {_roomFail,5}명");

            Console.WriteLine();
            PrintBottleneckAnalysis();
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine("  엔터를 누르면 메뉴로 돌아갑니다.");
            Console.ReadLine();
        }

        private static long Percentile(List<long> sorted, int p)
        {
            if (sorted.Count == 0)
                return 0;
            int idx = (int)Math.Ceiling(sorted.Count * p / 100.0) - 1;
            return sorted[Math.Clamp(idx, 0, sorted.Count - 1)];
        }

        private static void PrintBottleneckAnalysis()
        {
            Console.WriteLine($"  [병목 분석]");

            if (_loginFail > 10)
                Console.WriteLine($"    ⚠  로그인 실패 {_loginFail}건: Auth.API 또는 DB 상태 확인");

            if (_activeFail > 10)
                Console.WriteLine($"    ⚠  Active 획득 실패 {_activeFail}건: QueueWorkerService 처리 속도 확인");

            if (_matchTimeout > 5)
                Console.WriteLine($"    ⚠  매칭 타임아웃 {_matchTimeout}건: 홀수 생존자이거나 Matching.API 워커 지연");

            if (_matchFail > 0)
                Console.WriteLine($"    ⚠  매칭 실패 {_matchFail}건: Matching.API 또는 SignalR 상태 확인");

            if (_roomFail > 0)
                Console.WriteLine($"    ⚠  방 입장 실패 {_roomFail}건: Game.Server 동시 접속 부하 또는 Active 키 확인");

            int fullSuccessRate = USER_COUNT > 0 ? (_roomOk * 100 / USER_COUNT) : 0;
            if (fullSuccessRate >= 95)
                Console.WriteLine($"    OK  전체 성공률 {fullSuccessRate}% — 안정적");
            else if (fullSuccessRate >= 80)
                Console.WriteLine($"    --  전체 성공률 {fullSuccessRate}% — 튜닝 후 재테스트 권장");
            else
                Console.WriteLine($"    NG  전체 성공률 {fullSuccessRate}% — 병목 해소 필요");
        }

        private static void PrintHeader()
        {
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine("   [Scenario 5] 1000명 매칭 시스템 부하 테스트");
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine($"  유저 수: {USER_COUNT}명  │  스폰 속도: {SPAWN_RATE_PER_SEC}명/초");
            Console.WriteLine($"  계정명: {USERNAME_PREFIX}0001 ~ {USERNAME_PREFIX}{USER_COUNT:D4}");
            Console.WriteLine($"  매칭 타임아웃: {MATCH_WAIT_SEC}초");
            Console.WriteLine();
            Console.WriteLine("  필요 서버:");
            Console.WriteLine($"    - Auth.API      : {Consts.AUTH_API_URL}");
            Console.WriteLine($"    - Ticketing.API : {Consts.TICKET_API_URL}");
            Console.WriteLine($"    - Matching.API  : {Consts.MATCH_HUB_URL}");
            Console.WriteLine($"    - Game.Server   : {Consts.GAME_SERVER_IP}:{Consts.GAME_SERVER_PORT}");
            Console.WriteLine();
            Console.WriteLine("  테스트 시작 중...");
            Console.WriteLine("──────────────────────────────────────────────────────");
        }

        private static void ResetCounters()
        {
            _loginOk = _loginFail = 0;
            _queueOk = _queueFail = 0;
            _activeOk = _activeFail = 0;
            _tokenRefreshCount = 0;
            _matchOk = _matchTimeout = _matchFail = 0;
            _roomOk = _roomFail = 0;
            _completed = 0;
            while (_matchLatenciesMs.TryTake(out _))
            { }
        }
    }
}
