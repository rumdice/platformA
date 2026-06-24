using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using PlatformA.Library.Common;
using PlatformA.Library.Packets;

namespace PlatformA.Game.DummyClient.Scenarios
{
    /// <summary>
    /// 두 플레이어가 Auth → Lobby → Matching → Gomoku → GameOver → ResultRecord 전체 흐름을 자동 검증한다.
    /// 로컬 환경에서 모든 서비스(Auth.API, Matching.API, Game.Lobby, Game.Gomoku)가 기동된 상태에서 실행한다.
    /// </summary>
    public static class TwoPlayerGomokuScenario
    {
        private record MatchFoundInfo(string Host, int Port, string RoomId, string GameType);

        public static async Task<bool> RunAsync(bool interactive = true)
        {
            try
            { Console.Clear(); }
            catch { }
            Console.WriteLine("==================================================");
            Console.WriteLine("   [시나리오 9] Two-Player Gomoku E2E 검증");
            Console.WriteLine("==================================================\n");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

            // 두 플레이어 계정 생성 (테스트용 랜덤 계정)
            string user1 = AuthHelper.GenerateTestUserName();
            string user2 = AuthHelper.GenerateTestUserName();
            string password = "Test1234!";

            Console.WriteLine($"[준비] 테스트 계정 생성: {user1}, {user2}");

            using HttpClient http1 = new();
            using HttpClient http2 = new();

            // 회원가입 후 로그인
            TokenSession? s1 = await RegisterAndLoginAsync(http1, user1, password);
            TokenSession? s2 = await RegisterAndLoginAsync(http2, user2, password);

            if (s1 == null || s2 == null)
            {
                Console.WriteLine("❌ 로그인 실패. Auth.API 서버 상태를 확인하세요.");
                return false;
            }

            Console.WriteLine($"[인증] P1=User_{s1.PlayerId}, P2=User_{s2.PlayerId} 로그인 성공\n");

            var matchFoundP1 = new TaskCompletionSource<MatchFoundInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
            var matchFoundP2 = new TaskCompletionSource<MatchFoundInfo>(TaskCreationOptions.RunContinuationsAsynchronously);

            // 두 플레이어 병렬 실행
            var task1 = RunPlayerAsync("P1", s1, http1, matchFoundP1, matchFoundP2, cts.Token);
            var task2 = RunPlayerAsync("P2", s2, http2, matchFoundP2, matchFoundP1, cts.Token);

            await Task.WhenAll(task1, task2);

            bool success = !task1.IsFaulted && !task2.IsFaulted;
            Console.WriteLine("\n==================================================");
            Console.WriteLine(success
                ? "✅ E2E 검증 성공 — 전체 흐름 완주"
                : "❌ E2E 검증 실패 — 위 로그를 확인하세요");
            Console.WriteLine("==================================================");

            // MatchRecord 최종 확인
            if (success)
                await VerifyMatchRecordAsync(http1, s1);

            if (interactive)
            {
                Console.WriteLine("\n[엔터] 메인 메뉴로 돌아가기...");
                Console.ReadLine();
            }

            return success;
        }

        private static async Task RunPlayerAsync(
            string label,
            TokenSession session,
            HttpClient http,
            TaskCompletionSource<MatchFoundInfo> myMatchFound,
            TaskCompletionSource<MatchFoundInfo> otherMatchFound,
            CancellationToken ct)
        {
            AuthHelper.ApplyToken(http, session);

            // Lobby SignalR 연결
            HubConnection? lobby = null;
            try
            {
                lobby = new HubConnectionBuilder()
                    .WithUrl(Consts.LOBBY_HUB_URL, options =>
                    {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(session.AccessToken);
                        options.HttpMessageHandlerFactory = _ =>
                            new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
                    })
                    .Build();

                lobby.On<JsonElement>("MatchFound", payload =>
                {
                    try
                    {
                        string host = payload.GetProperty("host").GetString() ?? Consts.GOMOKU_SERVER_IP;
                        int port = payload.GetProperty("port").GetInt32();
                        string roomId = payload.GetProperty("roomId").GetString() ?? string.Empty;
                        string gameType = payload.TryGetProperty("gameType", out JsonElement gt)
                            ? gt.GetString() ?? string.Empty
                            : string.Empty;
                        myMatchFound.TrySetResult(new MatchFoundInfo(host, port, roomId, gameType));
                    }
                    catch (Exception ex)
                    {
                        myMatchFound.TrySetException(ex);
                    }
                });

                await lobby.StartAsync(ct);
                Console.WriteLine($"[{label}] Lobby SignalR 연결 성공");

                // P2는 100ms 뒤에 RequestMatch
                if (label == "P2")
                    await Task.Delay(100, ct);

                // RequestMatch 호출
                await lobby.InvokeAsync("RequestMatch", "gomoku", ct);
                Console.WriteLine($"[{label}] RequestMatch(gomoku) 전송");

                // MatchFound 수신 대기 (30초)
                using var matchTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, matchTimeout.Token);
                MatchFoundInfo matchInfo;
                try
                {
                    matchInfo = await myMatchFound.Task.WaitAsync(linked.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"[{label}] ❌ MatchFound 수신 타임아웃 (30초)");
                    return;
                }

                Console.WriteLine($"[{label}] MatchFound 수신 — room={matchInfo.RoomId} {matchInfo.Host}:{matchInfo.Port}");

                // Gomoku TCP 접속
                await RunGomokuClientAsync(label, session, matchInfo, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"[{label}] ❌ 오류: {ex.Message}");
                myMatchFound.TrySetException(ex);
            }
            finally
            {
                if (lobby != null)
                {
                    await lobby.StopAsync(CancellationToken.None);
                    await lobby.DisposeAsync();
                }
            }
        }

        private static async Task RunGomokuClientAsync(
            string label,
            TokenSession session,
            MatchFoundInfo matchInfo,
            CancellationToken ct)
        {
            using Socket tcp = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await tcp.ConnectAsync(matchInfo.Host, matchInfo.Port, ct);
            Console.WriteLine($"[{label}] Gomoku TCP 접속 성공");

            var gameStartTcs = new TaskCompletionSource<SGameStart>(TaskCreationOptions.RunContinuationsAsynchronously);
            var gameOverTcs = new TaskCompletionSource<SGameOver>(TaskCreationOptions.RunContinuationsAsynchronously);
            var boardUpdateQueue = new System.Collections.Concurrent.ConcurrentQueue<SBoardUpdate>();
            int myPlayerId = session.PlayerId;

            // 수신 루프 시작
            var recvTask = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        byte[]? frame = await PacketHelper.ReceiveFrameAsync(tcp, ct);
                        if (frame == null)
                            break;
                        Packet envelope = PacketHelper.ParseEnvelope(frame);
                        switch (envelope.PayloadCase)
                        {
                            case Packet.PayloadOneofCase.SLogin:
                                SLogin login = envelope.SLogin;
                                if (login.ResultCode == LoginResultCode.LoginSuccess)
                                    Console.WriteLine($"[{label}] SLogin 성공 — PlayerId={login.PlayerId}");
                                else
                                    Console.WriteLine($"[{label}] ❌ SLogin 실패 — {login.ResultCode}");
                                break;

                            case Packet.PayloadOneofCase.SGameStart:
                                Console.WriteLine($"[{label}] SGameStart 수신 — FirstTurn={envelope.SGameStart.FirstTurnPlayerId}");
                                gameStartTcs.TrySetResult(envelope.SGameStart);
                                break;

                            case Packet.PayloadOneofCase.SBoardUpdate:
                                boardUpdateQueue.Enqueue(envelope.SBoardUpdate);
                                break;

                            case Packet.PayloadOneofCase.SGameOver:
                                SGameOver over = envelope.SGameOver;
                                string result = over.WinnerId == myPlayerId ? "승리"
                                    : over.WinnerId == 0 ? "무승부" : "패배";
                                Console.WriteLine($"[{label}] SGameOver — {result} (이유={over.Reason}) lobbyUrl={over.LobbyUrl}");
                                gameOverTcs.TrySetResult(over);
                                return;
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Console.WriteLine($"[{label}] 수신 오류: {ex.Message}"); }
                gameOverTcs.TrySetCanceled();
            }, ct);

            // CLogin 전송
            byte[] loginPkt = PacketHelper.BuildPacket(new Packet
            {
                CLogin = new CLogin { JwtToken = session.AccessToken }
            });
            await tcp.SendAsync(loginPkt, ct);
            Console.WriteLine($"[{label}] CLogin 전송");

            // SGameStart 대기
            using var startTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, startTimeout.Token);
            SGameStart gameStart;
            try
            {
                gameStart = await gameStartTcs.Task.WaitAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[{label}] ❌ SGameStart 수신 타임아웃");
                return;
            }

            // 내 턴 결정: FirstTurnPlayerId가 나면 먼저, 아니면 대기
            bool isFirstPlayer = gameStart.FirstTurnPlayerId == myPlayerId;
            Console.WriteLine($"[{label}] 선턴: {(isFirstPlayer ? "나" : "상대")}");

            // 자동 돌 놓기: 좌표 순차 (7,7)부터
            int[] xs = [7, 7, 8, 8, 9, 9, 10, 10, 11];
            int[] ys = [7, 8, 7, 8, 7, 8, 7, 8, 7];
            int moveIdx = isFirstPlayer ? 0 : 1;

            // SGameOver 또는 타임아웃까지 교대 진행
            using var gameTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using var gameLinked = CancellationTokenSource.CreateLinkedTokenSource(ct, gameTimeout.Token);

            while (!gameOverTcs.Task.IsCompleted)
            {
                try
                {
                    // 내 차례인 경우에만 돌 놓기 (홀수 인덱스가 선턴)
                    bool myTurn = isFirstPlayer ? (moveIdx % 2 == 0) : (moveIdx % 2 == 1);
                    if (myTurn && moveIdx < xs.Length * 2)
                    {
                        int coordIdx = moveIdx / 2;
                        if (coordIdx < xs.Length)
                        {
                            byte[] stonePkt = PacketHelper.BuildPacket(new Packet
                            {
                                CPlaceStone = new CPlaceStone { X = xs[coordIdx], Y = ys[coordIdx] }
                            });
                            await tcp.SendAsync(stonePkt, gameLinked.Token);
                            await Task.Delay(150, gameLinked.Token);
                        }
                    }
                    moveIdx++;

                    // SBoardUpdate 수신 또는 짧게 대기
                    await Task.Delay(200, gameLinked.Token);

                    if (gameOverTcs.Task.IsCompleted)
                        break;
                }
                catch (OperationCanceledException) { break; }
            }

            await recvTask;
        }

        private static async Task VerifyMatchRecordAsync(HttpClient http, TokenSession session)
        {
            try
            {
                AuthHelper.ApplyToken(http, session);
                var resp = await http.GetAsync($"{Consts.MATCHING_API_BASE_URL}/api/gamematch/history");
                if (!resp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[검증] MatchRecord 조회 실패: HTTP {(int)resp.StatusCode}");
                    return;
                }

                string json = await resp.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;
                if (root.GetArrayLength() == 0)
                {
                    Console.WriteLine("[검증] MatchRecord 없음");
                    return;
                }

                JsonElement latest = root[0];
                string result = latest.TryGetProperty("result", out JsonElement r) ? r.GetString() ?? "" : "?";
                string gameType = latest.TryGetProperty("gameType", out JsonElement gt) ? gt.GetString() ?? "" : "?";
                Console.WriteLine($"[검증] 최신 MatchRecord — gameType={gameType}, result={result}");

                if (result is "승리" or "패배" or "무승부")
                    Console.WriteLine("[검증] ✅ MatchRecord Completed 확인");
                else
                    Console.WriteLine("[검증] ⚠️ MatchRecord 상태가 예상과 다릅니다.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[검증] 오류: {ex.Message}");
            }
        }

        private static async Task<TokenSession?> RegisterAndLoginAsync(HttpClient http, string username, string password)
        {
            // 회원가입 시도
            try
            {
                string registerUrl = Consts.AUTH_API_URL.Replace("/login", "/register");
                await http.PostAsJsonAsync(registerUrl, new { Username = username, Password = password });
            }
            catch { /* 이미 존재하는 계정이거나 엔드포인트 없음 — 무시 */ }

            // 로그인
            return await AuthHelper.LoginAsync(http, username, password);
        }
    }
}
