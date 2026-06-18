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

RedisManager.Instance.OnMatchSuccessReceived += (matchEvent) =>
{
    GameRoomManager.Instance.CreateRoom(matchEvent.RoomId);
};

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
