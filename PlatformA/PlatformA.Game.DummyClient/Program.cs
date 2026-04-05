using PlatformA.Game.DummyClient.Scenarios;

namespace PlatformA.Game.DummyClient
{

    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            while (true)
            {
                Console.Clear();
                Console.WriteLine("==================================================");
                Console.WriteLine("    🚀 PlatformA Integrated Simulator");
                Console.WriteLine("==================================================");
                Console.WriteLine(" 1. 게임 서버 직접 접속 (Auth + TCP)");
                Console.WriteLine(" 2. Auth 인증, 대기열, 인게임 서버 로그인. 200유저 부하 테스트");
                Console.WriteLine(" 3. [시나리오 3] 한명의 유저가 로그인 후 매칭 신청. 2개 실행.");
                Console.WriteLine(" 4. [시나리오 4] 1000명 로그인 + 대기열 통과 부하 테스트");
                Console.WriteLine(" 5. 종료");
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
                        return; // 프로그램 종료
                    default:
                        Console.WriteLine("\n[경고] 잘못된 입력입니다.");
                        await Task.Delay(1000);
                        break;
                }
            }
        }
    }
}
