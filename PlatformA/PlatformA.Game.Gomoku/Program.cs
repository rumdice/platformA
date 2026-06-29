using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using PlatformA.Game.Gomoku.Network;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Library.Game.Core;
using PlatformA.Library.Network;
using PlatformA.Library.Packets;

Console.WriteLine("=== PlatformA.Game.Gomoku Server (Port 7778) ===");

PacketManager<GomokuSession>.Instance.Register();

using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
RedisManager.Instance.Init(
    Consts.REDIS_CONNECTION_STRING,
    loggerFactory.CreateLogger<RedisManager>());

// 헬스체크 HTTP 서버 (포트 7779)
// /healthz → liveness: 프로세스 생존 여부만 확인 (항상 200)
// /readyz  → readiness: Matching.API /healthz 응답 확인
_ = Task.Run(async () =>
{
    using var httpListener = new HttpListener();
    httpListener.Prefixes.Add("http://+:7779/");
    httpListener.Start();
    Console.WriteLine("[Gomoku HealthCheck] Listening on http://+:7779/ (healthz + readyz)");

    using var httpClient = new HttpClient(
        new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        })
    {
        BaseAddress = new Uri(Consts.MATCHING_API_BASE_URL),
        Timeout = TimeSpan.FromSeconds(3),
    };

    while (true)
    {
        try
        {
            HttpListenerContext ctx = await httpListener.GetContextAsync();
            string path = ctx.Request.Url?.AbsolutePath ?? "";
            string json;
            int statusCode;

            if (path == "/healthz")
            {
                json = """{"status":"alive"}""";
                statusCode = 200;
            }
            else if (path == "/readyz")
            {
                try
                {
                    var resp = await httpClient.GetAsync("/healthz");
                    if (resp.IsSuccessStatusCode)
                    {
                        json = """{"status":"ready","matching_api":"ok"}""";
                        statusCode = 200;
                    }
                    else
                    {
                        json = $"""{"{"}"status":"degraded","matching_api":"http_{(int)resp.StatusCode}"{"}"}""";
                        statusCode = 503;
                    }
                }
                catch
                {
                    json = """{"status":"degraded","matching_api":"unreachable"}""";
                    statusCode = 503;
                }
            }
            else
            {
                json = """{"status":"not_found"}""";
                statusCode = 404;
            }

            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            ctx.Response.StatusCode = statusCode;
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = body.Length;
            await ctx.Response.OutputStream.WriteAsync(body);
            ctx.Response.Close();
        }
        catch (HttpListenerException)
        {
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HealthCheck] 오류: {ex.Message}");
        }
    }
});

using Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 7778);
listener.Bind(endPoint);
listener.Listen(1000);

Console.WriteLine($"[Gomoku Server] Listening on {endPoint}...");

while (true)
{
    try
    {
        Socket clientSocket = await listener.AcceptAsync();
        Session session = new GomokuSession();
        session.Start(clientSocket);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Gomoku Server Error] {ex.Message}");
        await Task.Delay(100);
    }
}
