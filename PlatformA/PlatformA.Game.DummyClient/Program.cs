using PlatformA.Game.DummyClient.Scenarios;

namespace PlatformA.Game.DummyClient
{

    internal class Program
    {
        static async Task Main(string[] args)
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("==================================================");
                Console.WriteLine("    🚀 PlatformA Integrated Simulator");
                Console.WriteLine("==================================================");
                Console.WriteLine(" 1. 게임 서버 직접 접속 (Auth + TCP)");
                Console.WriteLine(" 2. Auth 인증, 대기열, 인게임 서버 로그인");
                Console.WriteLine(" 3. [예정] 매칭 서버 연동 테스트 (SignalR)");
                Console.WriteLine(" 4. [예정]");
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
                        Console.WriteLine("\n[안내] 아직 준비 중인 기능입니다.");
                        await Task.Delay(1000);
                        break;
                    case "4":
                        Console.WriteLine("\n[안내] 아직 준비 중인 기능입니다.");
                        await Task.Delay(1000);
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
