using PlatformA.Game.DummyClient.Scenarios;

namespace PlatformA.Game.DummyClient
{

    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // ── Non-interactive E2E CLI 모드 ─────────────────────────────────
            if (args.Length > 0)
            {
                if (args[0] == "--list")
                {
                    Console.WriteLine("사용 가능한 E2E 시나리오:");
                    Console.WriteLine("  9   — [시나리오 9] Two-Player Gomoku E2E 검증");
                    Console.WriteLine("  all — 모든 E2E 시나리오 순차 실행");
                    Environment.Exit(0);
                    return;
                }

                if (args[0] == "--e2e" && args.Length > 1)
                {
                    string scenario = args[1];
                    string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    string logDir = Path.Combine(AppContext.BaseDirectory, "logs");
                    Directory.CreateDirectory(logDir);
                    string logPath = Path.Combine(logDir, $"e2e-{timestamp}.log");

                    var fileWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };
                    var tee = new TeeWriter(Console.Out, fileWriter);
                    Console.SetOut(tee);

                    Console.WriteLine($"[E2E] 시나리오 '{scenario}' 시작 — {timestamp}");
                    Console.WriteLine($"[E2E] 로그 파일: {logPath}");
                    Console.WriteLine();

                    bool success = false;
                    try
                    {
                        success = scenario switch
                        {
                            "9" => await TwoPlayerGomokuScenario.RunAsync(interactive: false),
                            "all" => await TwoPlayerGomokuScenario.RunAsync(interactive: false),
                            _ => throw new ArgumentException($"알 수 없는 시나리오 번호: {scenario}")
                        };
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[E2E] ❌ 예외 발생: {ex.Message}");
                        success = false;
                    }

                    Console.WriteLine();
                    Console.WriteLine(success
                        ? "[RESULT] SUCCESS"
                        : "[RESULT] FAILURE: 위 로그를 확인하세요");

                    tee.Dispose();
                    Environment.Exit(success ? 0 : 1);
                    return;
                }
            }

            // ── Interactive 메뉴 모드 ─────────────────────────────────────────
            while (true)
            {
                try
                { Console.Clear(); }
                catch { }
                Console.WriteLine("==================================================");
                Console.WriteLine("    🚀 Dummy Client Integrated Simulator");
                Console.WriteLine("==================================================");
                Console.WriteLine(" 1. [시나리오 1] 게임서버 직접 접속 테스트 (C# TCP Socket)");
                Console.WriteLine(" 2. [시나리오 2] Front Page Util.API 테스트.");
                Console.WriteLine(" 3. [시나리오 3] 한명의 유저가 로그인 후 매칭 신청. 2개 실행.");
                Console.WriteLine(" 4. [시나리오 4] 1000명 로그인 + 대기열 통과 부하 테스트");
                Console.WriteLine(" 5. [시나리오 5] 1000명 로그인 + 대기열 통과 부하 테스트 + 매칭 시스템 부하 테스트 (준비중)");
                Console.WriteLine(" 6. [시나리오 6] 매칭 시스템 레이팅별 매칭 테스트 (준비중)");
                Console.WriteLine(" 7. [시나리오 7] 단일 유저 로그인/재로그인/대기열 인증 통합 테스트");
                Console.WriteLine(" 8. [시나리오 8] 단일 유저 중복 로그인 방어 검증");
                Console.WriteLine(" 9. [시나리오 9] 두 명 자동 매칭 → Gomoku 게임 완주 E2E 검증");
                Console.WriteLine(" 0. 종료");
                Console.WriteLine("==================================================");
                Console.Write("원하는 테스트 모드의 번호를 입력하세요: ");

                string input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        await GameClientScenario.RunAsync();
                        break;
                    case "2":
                        await TicketingScenario.RunAsync();
                        break;
                    case "3":
                        await MatchingScenario.RunAsync();
                        break;
                    case "4":
                        await LoginWaitScenario_1.RunAsync();
                        break;
                    case "5":
                        await LoadTestMatchingScenario.RunAsync();
                        break;
                    case "6":
                        Console.WriteLine("\n[경고] 준비중인 기능입니다.");
                        await Task.Delay(500);
                        break;
                    case "7":
                        await LoginWaitScenario_2.RunAsync();
                        break;
                    case "8":
                        await DuplicateLoginScenario.RunAsync();
                        break;
                    case "9":
                        await TwoPlayerGomokuScenario.RunAsync();
                        break;
                    case "0":
                        return;
                    default:
                        Console.WriteLine("\n[경고] 잘못된 입력입니다.");
                        await Task.Delay(1000);
                        break;
                }
            }
        }
    }
}
