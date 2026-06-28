using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using PlatformA.Library.Common;
using PlatformA.Library.Packets;

namespace PlatformA.Game.DummyClient.Scenarios
{
    /// <summary>
    /// 시나리오 10: 1000명 동시 Gomoku E2E 검증
    ///
    /// 흐름 (정상 유저):
    ///   Auth 로그인 → 대기열 진입 → Active 대기 → Lobby SignalR →
    ///   RequestMatch → MatchFound → Gomoku TCP → 게임 완주 → MatchRecord 검증
    ///
    /// Failover (각 5%):
    ///   A — Active 후 Lobby 미진입 (orphan queue entry)
    ///   B — MatchFound 후 TCP 미접속 (ghost match room)
    ///   C — SGameStart 후 TCP 강제 종료 (abandoned mid-game)
    /// </summary>
    public static class MassGomokuE2EScenario
    {
        // ── 파라미터 ──────────────────────────────────────────────────────────────
        private const int USER_COUNT = 1000;
        private const int SPAWN_RATE = 50;               // 명/초
        private const int SPAWN_INTERVAL_MS = 1000 / SPAWN_RATE;
        private const string USERNAME_PREFIX = "mass_e2e_";
        private const string PASSWORD = "Test1234!";
        private const int MAX_GAME_CONCURRENCY = 200;
        private const int MATCH_TIMEOUT_SEC = 60;
        private const int GAME_TIMEOUT_SEC = 90;
        private const int TOTAL_TIMEOUT_SEC = 600;

        private const double FO_A = 0.05;
        private const double FO_B = 0.05;
        private const double FO_C = 0.05;

        // ── 유저 타입 ──────────────────────────────────────────────────────────────
        private enum UserType { Normal, FailoverA, FailoverB, FailoverC }

        // ── 측정 지표 ──────────────────────────────────────────────────────────────
        private static int _loginOk, _loginFail;
        private static int _queueOk, _queueFail;
        private static int _activeOk, _activeFail;
        private static int _failoverA;
        private static int _lobbyOk, _lobbyFail;
        private static int _matchReq, _matchOk, _matchTimeout;
        private static int _failoverB;
        private static int _tcpOk, _tcpFail;
        private static int _gameStartOk;
        private static int _failoverC;
        private static int _gameOverOk, _gameOverFail;
        private static int _win, _lose, _draw;
        private static int _verifyOk, _verifyFail;
        private static int _completed;

        // ── 공유 HTTP 핸들러 ───────────────────────────────────────────────────────
        private static readonly SocketsHttpHandler _sockHandler = new()
        {
            SslOptions = { RemoteCertificateValidationCallback = (_, _, _, _) => true },
            MaxConnectionsPerServer = 100,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        };

        private static readonly SemaphoreSlim _gameSem = new(MAX_GAME_CONCURRENCY, MAX_GAME_CONCURRENCY);

        // ── 진입점 ────────────────────────────────────────────────────────────────
        public static async Task<bool> RunAsync(bool interactive = false)
        {
            Reset();
            PrintHeader();

            string startTimestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var sw = Stopwatch.StartNew();
            using var rootCts = new CancellationTokenSource(TimeSpan.FromSeconds(TOTAL_TIMEOUT_SEC));
            var ct = rootCts.Token;

            using var displayCts = new CancellationTokenSource();
            var displayTask = LiveProgressAsync(displayCts.Token);

            var tasks = new List<Task>(USER_COUNT);
            for (int i = 1; i <= USER_COUNT; i++)
            {
                int uid = i;
                tasks.Add(RunUserAsync(uid, Classify(uid), ct));
                try
                { await Task.Delay(SPAWN_INTERVAL_MS, ct); }
                catch (OperationCanceledException) { break; }
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            displayCts.Cancel();
            try
            { await displayTask; }
            catch (OperationCanceledException) { }

            bool passed = PrintReport(sw.Elapsed, startTimestamp);

            if (interactive)
            {
                Console.WriteLine("\n[엔터] 메인 메뉴로 돌아가기...");
                Console.ReadLine();
            }
            return passed;
        }

        // ── 유저 타입 결정 (해시 기반 결정론적 분배) ─────────────────────────────
        private static UserType Classify(int uid)
        {
            double h = (uid * 2654435761u & 0xFFFFFFFFu) / (double)0xFFFFFFFFu;
            if (h < FO_A)
                return UserType.FailoverA;
            if (h < FO_A + FO_B)
                return UserType.FailoverB;
            if (h < FO_A + FO_B + FO_C)
                return UserType.FailoverC;
            return UserType.Normal;
        }

        // ── 유저 라이프사이클 ─────────────────────────────────────────────────────
        private static async Task RunUserAsync(int uid, UserType type, CancellationToken ct)
        {
            string username = $"{USERNAME_PREFIX}{uid:D4}";
            string fakeIp = $"10.{uid / 256}.{uid % 256}.1";

            using var http = new HttpClient(_sockHandler, disposeHandler: false)
            {
                Timeout = TimeSpan.FromSeconds(30),
            };
            http.DefaultRequestHeaders.Add("X-Forwarded-For", fakeIp);

            try
            {
                // Stage 1 — Register + Login
                var session = await RegisterAndLoginAsync(http, username, ct);
                if (session == null)
                { Interlocked.Increment(ref _loginFail); return; }
                Interlocked.Increment(ref _loginOk);

                // Stage 2 — 대기열 진입
                AuthHelper.ApplyToken(http, session);
                if (!await EnterQueueAsync(http, ct))
                { Interlocked.Increment(ref _queueFail); return; }
                Interlocked.Increment(ref _queueOk);

                // Stage 3 — Active 대기
                var (activated, latestSession, _) = await AuthHelper.WaitUntilActiveAsync(http, session, 300);
                session = latestSession;
                if (!activated)
                { Interlocked.Increment(ref _activeFail); return; }
                Interlocked.Increment(ref _activeOk);

                // Failover A — Active 후 의도적 중단 (orphan queue 처리 검증)
                if (type == UserType.FailoverA)
                {
                    Interlocked.Increment(ref _failoverA);
                    return;
                }

                // Stage 4–9 — Lobby + Match + Game (동시성 제한)
                await _gameSem.WaitAsync(ct);
                try
                { await RunGameStageAsync(uid, type, session, http, ct); }
                finally { _gameSem.Release(); }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (_loginFail + _tcpFail < 5)
                    Console.WriteLine($"  [User{uid:D4}] 예외: {ex.GetType().Name} — {ex.Message}");
            }
            finally { Interlocked.Increment(ref _completed); }
        }

        private static async Task RunGameStageAsync(
            int uid, UserType type, TokenSession session, HttpClient http, CancellationToken ct)
        {
            AuthHelper.ApplyToken(http, session);
            HubConnection? lobby = null;

            try
            {
                // Stage 4 — Lobby SignalR
                var matchTcs = new TaskCompletionSource<MatchInfo>(TaskCreationOptions.RunContinuationsAsynchronously);

                lobby = new HubConnectionBuilder()
                    .WithUrl(Consts.LOBBY_HUB_URL, opts =>
                    {
                        opts.AccessTokenProvider = () => Task.FromResult<string?>(session.AccessToken);
                        opts.HttpMessageHandlerFactory = _ =>
                            new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
                    })
                    .Build();

                lobby.On<JsonElement>("MatchFound", payload =>
                {
                    try
                    {
                        matchTcs.TrySetResult(new MatchInfo(
                            payload.GetProperty("host").GetString() ?? Consts.GOMOKU_SERVER_IP,
                            payload.GetProperty("port").GetInt32(),
                            payload.GetProperty("roomId").GetString() ?? ""));
                    }
                    catch (Exception ex) { matchTcs.TrySetException(ex); }
                });

                try
                {
                    await lobby.StartAsync(ct);
                    Interlocked.Increment(ref _lobbyOk);
                }
                catch
                {
                    Interlocked.Increment(ref _lobbyFail);
                    return;
                }

                // Stage 5 — RequestMatch
                await lobby.InvokeAsync("RequestMatch", "gomoku", ct);
                Interlocked.Increment(ref _matchReq);

                // Stage 6 — MatchFound 대기
                using var matchCts = new CancellationTokenSource(TimeSpan.FromSeconds(MATCH_TIMEOUT_SEC));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, matchCts.Token);
                MatchInfo matchInfo;
                try
                {
                    matchInfo = await matchTcs.Task.WaitAsync(linked.Token);
                    Interlocked.Increment(ref _matchOk);
                }
                catch (OperationCanceledException)
                {
                    Interlocked.Increment(ref _matchTimeout);
                    return;
                }

                // Failover B — MatchFound 수신 후 연결 끊김 (ghost room 처리 검증)
                if (type == UserType.FailoverB)
                {
                    Interlocked.Increment(ref _failoverB);
                    return; // Lobby 연결만 끊고 TCP 미접속
                }

                // Stage 7 — Gomoku TCP 접속
                using var tcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await tcp.ConnectAsync(matchInfo.Host, matchInfo.Port, ct);
                    Interlocked.Increment(ref _tcpOk);
                }
                catch
                {
                    Interlocked.Increment(ref _tcpFail);
                    return;
                }

                // Stage 8 — CLogin → SGameStart
                var startTcs = new TaskCompletionSource<SGameStart>(TaskCreationOptions.RunContinuationsAsynchronously);
                var overTcs = new TaskCompletionSource<SGameOver>(TaskCreationOptions.RunContinuationsAsynchronously);

                var recvTask = Task.Run(async () =>
                {
                    try
                    {
                        while (true)
                        {
                            byte[]? frame = await PacketHelper.ReceiveFrameAsync(tcp, ct);
                            if (frame == null)
                                break;
                            var env = PacketHelper.ParseEnvelope(frame);
                            switch (env.PayloadCase)
                            {
                                case Packet.PayloadOneofCase.SLogin:
                                    break;
                                case Packet.PayloadOneofCase.SGameStart:
                                    startTcs.TrySetResult(env.SGameStart);
                                    break;
                                case Packet.PayloadOneofCase.SBoardUpdate:
                                    break;
                                case Packet.PayloadOneofCase.SGameOver:
                                    overTcs.TrySetResult(env.SGameOver);
                                    return;
                            }
                        }
                    }
                    catch { }
                    overTcs.TrySetCanceled();
                }, ct);

                byte[] loginPkt = PacketHelper.BuildPacket(new Packet { CLogin = new CLogin { JwtToken = session.AccessToken } });
                await tcp.SendAsync(loginPkt, ct);

                using var startCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                using var startLinked = CancellationTokenSource.CreateLinkedTokenSource(ct, startCts.Token);
                SGameStart gameStart;
                try
                { gameStart = await startTcs.Task.WaitAsync(startLinked.Token); }
                catch (OperationCanceledException) { return; }
                Interlocked.Increment(ref _gameStartOk);

                // Failover C — SGameStart 직후 TCP 강제 종료 (abandoned game 처리 검증)
                if (type == UserType.FailoverC)
                {
                    Interlocked.Increment(ref _failoverC);
                    tcp.Close();
                    return;
                }

                // Stage 9 — 자동 게임 진행
                bool isFirst = gameStart.FirstTurnPlayerId == session.PlayerId;
                int[] xs = [7, 7, 8, 8, 9, 9, 10, 10, 11];
                int[] ys = [7, 8, 7, 8, 7, 8, 7, 8, 7];
                int moveIdx = isFirst ? 0 : 1;

                using var gameCts = new CancellationTokenSource(TimeSpan.FromSeconds(GAME_TIMEOUT_SEC));
                using var gameLinked = CancellationTokenSource.CreateLinkedTokenSource(ct, gameCts.Token);

                while (!overTcs.Task.IsCompleted && !gameLinked.Token.IsCancellationRequested)
                {
                    try
                    {
                        bool myTurn = isFirst ? (moveIdx % 2 == 0) : (moveIdx % 2 == 1);
                        if (myTurn)
                        {
                            int ci = moveIdx / 2;
                            if (ci < xs.Length)
                            {
                                byte[] pkt = PacketHelper.BuildPacket(new Packet
                                { CPlaceStone = new CPlaceStone { X = xs[ci], Y = ys[ci] } });
                                await tcp.SendAsync(pkt, gameLinked.Token);
                            }
                        }
                        moveIdx++;
                        await Task.Delay(200, gameLinked.Token);
                    }
                    catch (OperationCanceledException) { break; }
                }

                await recvTask;

                if (overTcs.Task.IsCompletedSuccessfully)
                {
                    Interlocked.Increment(ref _gameOverOk);
                    var over = overTcs.Task.Result;
                    if (over.WinnerId == session.PlayerId)
                        Interlocked.Increment(ref _win);
                    else if (over.WinnerId == 0)
                        Interlocked.Increment(ref _draw);
                    else
                        Interlocked.Increment(ref _lose);

                    // Stage 10 — MatchRecord 검증
                    AuthHelper.ApplyToken(http, session);
                    bool verified = await VerifyMatchRecordAsync(http, ct);
                    if (verified)
                        Interlocked.Increment(ref _verifyOk);
                    else
                        Interlocked.Increment(ref _verifyFail);
                }
                else
                {
                    Interlocked.Increment(ref _gameOverFail);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (_gameOverFail < 3)
                    Console.WriteLine($"  [User{uid:D4}] 게임 오류: {ex.Message}");
            }
            finally
            {
                if (lobby != null)
                {
                    try
                    { await lobby.StopAsync(CancellationToken.None); }
                    catch { }
                    try
                    { await lobby.DisposeAsync(); }
                    catch { }
                }
            }
        }

        // ── 헬퍼 ──────────────────────────────────────────────────────────────────
        private static async Task<TokenSession?> RegisterAndLoginAsync(
            HttpClient http, string username, CancellationToken ct)
        {
            try
            {
                string regUrl = Consts.AUTH_API_URL.Replace("/login", "/register");
                await http.PostAsJsonAsync(regUrl, new { Username = username, Password = PASSWORD }, ct);
            }
            catch { }
            return await AuthHelper.LoginAsync(http, username, PASSWORD);
        }

        private static async Task<bool> EnterQueueAsync(HttpClient http, CancellationToken ct)
        {
            try
            {
                var resp = await http.PostAsync($"{Consts.TICKET_API_URL}/api/queue/enter", null, ct);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        private static async Task<bool> VerifyMatchRecordAsync(HttpClient http, CancellationToken ct)
        {
            try
            {
                var resp = await http.GetAsync($"{Consts.MATCHING_API_BASE_URL}/api/gamematch/history", ct);
                if (!resp.IsSuccessStatusCode)
                    return false;
                var json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetArrayLength() > 0;
            }
            catch { return false; }
        }

        // ── 실시간 진행 출력 ───────────────────────────────────────────────────────
        private static async Task LiveProgressAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(5000, ct);
                    Console.WriteLine(
                        $"[{DateTime.Now:HH:mm:ss}] 완료:{_completed,4}/{USER_COUNT} │ " +
                        $"로그인:{_loginOk,4}✔{_loginFail,3}✗ │ " +
                        $"Active:{_activeOk,4}✔{_activeFail,3}✗ │ " +
                        $"Lobby:{_lobbyOk,4}✔{_lobbyFail,3}✗ │ " +
                        $"매칭:{_matchOk,4}✔{_matchTimeout,3}⌛ │ " +
                        $"게임완주:{_gameOverOk,4}✔{_gameOverFail,3}✗ │ " +
                        $"FO-A:{_failoverA} FO-B:{_failoverB} FO-C:{_failoverC}");
                }
            }
            catch (OperationCanceledException) { }
        }

        // ── 최종 리포트 ────────────────────────────────────────────────────────────
        private static bool PrintReport(TimeSpan elapsed, string startTimestamp)
        {
            int foTotal = (int)(USER_COUNT * (FO_A + FO_B + FO_C));
            int normalExpected = USER_COUNT - foTotal;

            double loginRate = (double)_loginOk / USER_COUNT * 100;
            double activeRate = (double)_activeOk / USER_COUNT * 100;
            double gameRate = normalExpected > 0
                ? (double)_gameOverOk / normalExpected * 100
                : 0;

            Console.WriteLine();
            Console.WriteLine("══════════════════════════════════════════════════════════════════");
            Console.WriteLine("  Mass Gomoku E2E — 최종 결과 리포트");
            Console.WriteLine("══════════════════════════════════════════════════════════════════");
            Console.WriteLine($"  총 소요 시간 : {elapsed.TotalSeconds:F1}초 ({elapsed:mm\\:ss})");
            Console.WriteLine($"  총 유저 수   : {USER_COUNT}명  (정상:{normalExpected} FO-A:{(int)(USER_COUNT * FO_A)} FO-B:{(int)(USER_COUNT * FO_B)} FO-C:{(int)(USER_COUNT * FO_C)})");
            Console.WriteLine();

            Console.WriteLine("  ┌─ STAGE 1 : Auth.API 로그인 ─────────────────────────────────");
            Console.WriteLine($"  │  성공 {_loginOk,5} / 실패 {_loginFail,5}  ({loginRate:F1}%)");
            Console.WriteLine();

            Console.WriteLine("  ┌─ STAGE 2 : 대기열 진입 ────────────────────────────────────");
            Console.WriteLine($"  │  성공 {_queueOk,5} / 실패 {_queueFail,5}");
            Console.WriteLine();

            Console.WriteLine("  ┌─ STAGE 3 : Active 대기 ────────────────────────────────────");
            Console.WriteLine($"  │  성공 {_activeOk,5} / 실패 {_activeFail,5}  ({activeRate:F1}%)");
            Console.WriteLine();

            Console.WriteLine("  ┌─ FAILOVER-A : Active 후 Lobby 미진입 ──────────────────────");
            Console.WriteLine($"  │  대상 {_failoverA,5}명 — orphan session 서버측 처리 검증");
            Console.WriteLine();

            Console.WriteLine("  ┌─ STAGE 4 : Lobby SignalR 연결 ─────────────────────────────");
            Console.WriteLine($"  │  성공 {_lobbyOk,5} / 실패 {_lobbyFail,5}");
            Console.WriteLine();

            Console.WriteLine("  ┌─ STAGE 5+6 : RequestMatch → MatchFound ────────────────────");
            Console.WriteLine($"  │  요청 {_matchReq,5}  매칭성사 {_matchOk,5}  타임아웃 {_matchTimeout,5}");
            Console.WriteLine();

            Console.WriteLine("  ┌─ FAILOVER-B : MatchFound 후 TCP 미접속 ────────────────────");
            Console.WriteLine($"  │  대상 {_failoverB,5}명 — ghost match room 처리 검증");
            Console.WriteLine();

            Console.WriteLine("  ┌─ STAGE 7+8 : Gomoku TCP → SGameStart ──────────────────────");
            Console.WriteLine($"  │  TCP:{_tcpOk,5}✔ {_tcpFail,3}✗  GameStart:{_gameStartOk,5}");
            Console.WriteLine();

            Console.WriteLine("  ┌─ FAILOVER-C : 게임 시작 후 TCP 강제 종료 ──────────────────");
            Console.WriteLine($"  │  대상 {_failoverC,5}명 — abandoned game room 처리 검증");
            Console.WriteLine();

            Console.WriteLine("  ┌─ STAGE 9 : 게임 완주 ──────────────────────────────────────");
            Console.WriteLine($"  │  완주 {_gameOverOk,5}  실패/타임아웃 {_gameOverFail,5}  ({gameRate:F1}%)");
            Console.WriteLine($"  │  결과 분포 — 승:{_win} 패:{_lose} 무:{_draw}");
            Console.WriteLine();

            Console.WriteLine("  ┌─ STAGE 10 : MatchRecord 검증 ──────────────────────────────");
            Console.WriteLine($"  │  성공 {_verifyOk,5} / 실패 {_verifyFail,5}");
            Console.WriteLine();

            Console.WriteLine("  ┌─ Failover 검증 결과 ────────────────────────────────────────");
            Console.WriteLine($"  │  FO-A(orphan)  실행:{_failoverA}  목표:{(int)(USER_COUNT * FO_A)}  {(_failoverA > 0 ? "✅" : "⚠")}");
            Console.WriteLine($"  │  FO-B(ghost)   실행:{_failoverB}  목표:{(int)(USER_COUNT * FO_B)}  {(_failoverB > 0 ? "✅" : "⚠")}");
            Console.WriteLine($"  │  FO-C(abandon) 실행:{_failoverC}  목표:{(int)(USER_COUNT * FO_C)}  {(_failoverC > 0 ? "✅" : "⚠")}");
            Console.WriteLine();

            bool loginPass = loginRate >= 90.0;
            bool activePass = activeRate >= 85.0;
            bool gamePass = gameRate >= 70.0;
            bool foPass = _failoverA > 0 && _failoverB > 0 && _failoverC > 0;
            bool passed = loginPass && activePass && gamePass && foPass;

            Console.WriteLine("  ── 종합 판정 ─────────────────────────────────────────────────");
            Console.WriteLine($"  로그인 성공률  : {loginRate:F1}%  (목표 ≥90%)  {(loginPass ? "✅" : "❌")}");
            Console.WriteLine($"  Active 성공률  : {activeRate:F1}%  (목표 ≥85%)  {(activePass ? "✅" : "❌")}");
            Console.WriteLine($"  게임 완주율    : {gameRate:F1}%  (목표 ≥70%)  {(gamePass ? "✅" : "❌")}");
            Console.WriteLine($"  Failover 검증  : A/B/C 모두 실행됨  {(foPass ? "✅" : "❌")}");
            Console.WriteLine();
            Console.WriteLine(passed
                ? "  ✅ OVERALL PASS — 전체 E2E 검증 성공"
                : "  ❌ OVERALL FAIL — 기준 미달 항목 있음");
            Console.WriteLine("══════════════════════════════════════════════════════════════════");

            SaveJsonReport(elapsed, startTimestamp, passed);

            return passed;
        }

        private static void SaveJsonReport(TimeSpan elapsed, string startTimestamp, bool passed)
        {
            try
            {
                var report = new
                {
                    scenario = "MassGomokuE2E",
                    date = DateTime.UtcNow.ToString("o"),
                    userCount = USER_COUNT,
                    spawnRate = SPAWN_RATE,
                    maxGameConcurrency = MAX_GAME_CONCURRENCY,
                    totalElapsedSeconds = Math.Round(elapsed.TotalSeconds, 1),
                    loginOk = _loginOk,
                    loginFail = _loginFail,
                    queueOk = _queueOk,
                    queueFail = _queueFail,
                    activeOk = _activeOk,
                    activeFail = _activeFail,
                    lobbyOk = _lobbyOk,
                    lobbyFail = _lobbyFail,
                    matchReq = _matchReq,
                    matchOk = _matchOk,
                    matchTimeout = _matchTimeout,
                    tcpOk = _tcpOk,
                    tcpFail = _tcpFail,
                    gameStartOk = _gameStartOk,
                    gameOverOk = _gameOverOk,
                    gameOverFail = _gameOverFail,
                    verifyOk = _verifyOk,
                    verifyFail = _verifyFail,
                    win = _win,
                    lose = _lose,
                    draw = _draw,
                    failoverA = _failoverA,
                    failoverB = _failoverB,
                    failoverC = _failoverC,
                    passed,
                };
                string reportDir = Path.Combine(AppContext.BaseDirectory, "reports");
                Directory.CreateDirectory(reportDir);
                string reportPath = Path.Combine(reportDir, $"e2e-{startTimestamp}.json");
                File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"  리포트 저장: {reportPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [경고] JSON 리포트 저장 실패: {ex.Message}");
            }
        }

        private static void PrintHeader()
        {
            Console.WriteLine("══════════════════════════════════════════════════════════════════");
            Console.WriteLine("  [시나리오 10] Mass Gomoku E2E — 1000명 동시 전체 흐름 + Failover");
            Console.WriteLine("══════════════════════════════════════════════════════════════════");
            Console.WriteLine($"  유저:{USER_COUNT}명  스폰:{SPAWN_RATE}/초  동시게임:{MAX_GAME_CONCURRENCY}  타임아웃:{TOTAL_TIMEOUT_SEC}초");
            Console.WriteLine($"  Failover-A(orphan):{FO_A * 100:F0}%  Failover-B(ghost):{FO_B * 100:F0}%  Failover-C(abandon):{FO_C * 100:F0}%");
            Console.WriteLine();
        }

        private static void Reset()
        {
            _loginOk = _loginFail = 0;
            _queueOk = _queueFail = 0;
            _activeOk = _activeFail = 0;
            _failoverA = _failoverB = _failoverC = 0;
            _lobbyOk = _lobbyFail = 0;
            _matchReq = _matchOk = _matchTimeout = 0;
            _tcpOk = _tcpFail = 0;
            _gameStartOk = 0;
            _gameOverOk = _gameOverFail = 0;
            _win = _lose = _draw = 0;
            _verifyOk = _verifyFail = 0;
            _completed = 0;
        }

        private record MatchInfo(string Host, int Port, string RoomId);
    }
}
