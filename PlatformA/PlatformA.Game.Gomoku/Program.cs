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

// 헬스체크 HTTP 서버 (포트 7779) — K8s liveness probe용
_ = Task.Run(async () =>
{
    using var httpListener = new HttpListener();
    httpListener.Prefixes.Add("http://+:7779/");
    httpListener.Start();
    Console.WriteLine("[Gomoku HealthCheck] Listening on http://+:7779/healthz");
    while (true)
    {
        try
        {
            HttpListenerContext ctx = await httpListener.GetContextAsync();
            bool isHealthz = ctx.Request.Url?.AbsolutePath == "/healthz";
            byte[] body = System.Text.Encoding.UTF8.GetBytes(isHealthz ? "Healthy" : "Not Found");
            ctx.Response.StatusCode = isHealthz ? 200 : 404;
            ctx.Response.ContentLength64 = body.Length;
            await ctx.Response.OutputStream.WriteAsync(body);
            ctx.Response.Close();
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
