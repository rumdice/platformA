using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace PlatformA.Game.DummyClient
{
    internal sealed record ServiceSpec(
        string Name,
        string Project,
        string HealthUrl,
        string LaunchProfile = "",
        bool SkipSsl = false,
        int TcpPort = 0);  // TcpPort > 0 이면 HTTP 대신 TCP 연결로 헬스체크

    internal static class ServiceManager
    {
        private static readonly ServiceSpec[] _specs =
        [
            new("Auth.API",      "PlatformA/PlatformA.Auth.API",      "https://localhost:7001/healthz", "https", SkipSsl: true),
            new("Ticketing.API", "PlatformA/PlatformA.Ticketing.API", "https://localhost:7003/healthz", "https", SkipSsl: true),
            new("Matching.API",  "PlatformA/PlatformA.Matching.API",  "https://localhost:7002/healthz", "https", SkipSsl: true),
            new("Game.Lobby",    "PlatformA/PlatformA.Game.Lobby",    "http://localhost:7777/healthz"),
            new("Game.Gomoku",   "PlatformA/PlatformA.Game.Gomoku",   "http://localhost:7779/healthz", TcpPort: 7778),
        ];

        private static readonly List<(string Name, Process Proc)> _started = [];
        private static string _repoRoot = Directory.GetCurrentDirectory();

        public static async Task<bool> EnsureAllRunningAsync(string repoRoot, CancellationToken ct = default)
        {
            _repoRoot = repoRoot;
            Console.WriteLine("[ServiceManager] 서비스 상태 점검...");

            // 이미 실행 중인 서비스는 종료 후 재시작
            foreach (var spec in _specs)
            {
                bool alive = await PingAsync(spec, ct);
                if (alive)
                {
                    Console.WriteLine($"  ⚠ {spec.Name} 실행 중 → 종료 후 재시작");
                    KillByPort(spec);
                    await Task.Delay(1000, ct);
                }
            }

            // 전체 재시작
            foreach (var spec in _specs)
            {
                Console.Write($"  ⚙ {spec.Name} 시작 중...");
                var proc = Launch(spec);
                if (proc == null)
                { Console.WriteLine(" ❌ 실패"); return false; }
                _started.Add((spec.Name, proc));
                Console.WriteLine($" PID={proc.Id}");
            }

            Console.WriteLine("[ServiceManager] 헬스체크 대기 (최대 180초)...");
            bool ready = await WaitAllHealthyAsync(ct, 180);
            if (ready)
                Console.WriteLine("[ServiceManager] ✅ 전체 서비스 준비 완료\n");
            else
                Console.WriteLine("[ServiceManager] ❌ 타임아웃 — 일부 서비스 미준비\n");
            return ready;
        }

        public static void StopStarted()
        {
            if (_started.Count == 0)
                return;
            Console.WriteLine("\n[ServiceManager] 테스트 서비스 종료 중...");
            foreach (var (name, proc) in _started)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill(entireProcessTree: true);
                        proc.WaitForExit(5000);
                    }
                    Console.WriteLine($"  ✔ {name} 종료됨");
                }
                catch (Exception ex) { Console.WriteLine($"  ⚠ {name} 종료 실패: {ex.Message}"); }
                finally { proc.Dispose(); }
            }
            _started.Clear();
        }

        private static Process? Launch(ServiceSpec spec)
        {
            try
            {
                string projPath = Path.Combine(_repoRoot, spec.Project.Replace('/', Path.DirectorySeparatorChar));
                string args = string.IsNullOrEmpty(spec.LaunchProfile)
                    ? $"run -c Release --no-build --project \"{projPath}\""
                    : $"run -c Release --no-build --project \"{projPath}\" --launch-profile {spec.LaunchProfile}";

                var psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = true,
                    WorkingDirectory = _repoRoot,
                };
                return Process.Start(psi);
            }
            catch (Exception ex)
            {
                Console.WriteLine($" [{spec.Name}] 시작 오류: {ex.Message}");
                return null;
            }
        }

        private static void KillByPort(ServiceSpec spec)
        {
            // TcpPort 우선, 없으면 healthz URL 포트에서 추출
            int port = spec.TcpPort > 0
                ? spec.TcpPort
                : (string.IsNullOrEmpty(spec.HealthUrl) ? 0 : new Uri(spec.HealthUrl).Port);
            if (port == 0)
                return;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-Command \"Get-NetTCPConnection -LocalPort {port} -ErrorAction SilentlyContinue | ForEach-Object {{ Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(5000);
            }
            catch { }
        }

        private static async Task<bool> WaitAllHealthyAsync(CancellationToken ct, int timeoutSec)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);

            int attempt = 0;
            while (!linked.Token.IsCancellationRequested)
            {
                attempt++;
                bool[] results = await Task.WhenAll(_specs.Select(s => PingAsync(s, CancellationToken.None)));
                int healthy = results.Count(r => r);
                if (healthy == _specs.Length)
                    return true;
                if (attempt % 5 == 0)
                    Console.WriteLine($"  [{attempt * 2}s] {healthy}/{_specs.Length} 준비됨");
                try
                { await Task.Delay(2000, linked.Token); }
                catch (OperationCanceledException) { break; }
            }

            // 최종 상태 출력
            for (int i = 0; i < _specs.Length; i++)
            {
                bool ok = await PingAsync(_specs[i], CancellationToken.None);
                Console.WriteLine($"  {(ok ? "✔" : "✗")} {_specs[i].Name}");
            }
            return false;
        }

        internal static async Task<bool> PingAsync(ServiceSpec spec, CancellationToken ct)
        {
            // TCP 포트 체크 (HttpListener 권한 문제를 우회)
            if (spec.TcpPort > 0)
            {
                return await PingTcpAsync(spec.TcpPort, ct);
            }
            // HTTP 헬스체크
            try
            {
                using var handler = new HttpClientHandler();
                if (spec.SkipSsl)
                    handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
                using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };
                var resp = await http.GetAsync(spec.HealthUrl, ct);
                return resp.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable;
            }
            catch { return false; }
        }

        private static async Task<bool> PingTcpAsync(int port, CancellationToken ct)
        {
            try
            {
                using var tcp = new TcpClient();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(3));
                await tcp.ConnectAsync("127.0.0.1", port, cts.Token);
                return true;
            }
            catch { return false; }
        }
    }
}
