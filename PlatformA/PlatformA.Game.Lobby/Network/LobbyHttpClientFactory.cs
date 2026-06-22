using Microsoft.Extensions.DependencyInjection;

namespace PlatformA.Game.Lobby.Network
{
    /// <summary>
    /// PacketHandler(static)에서 IHttpClientFactory에 접근하기 위한 싱글턴 브리지.
    /// Program.cs에서 DI 빌드 직후 한 번 초기화된다.
    /// </summary>
    public static class LobbyHttpClientFactory
    {
        public static IHttpClientFactory? Instance { get; private set; }

        public static void Initialize(IHttpClientFactory factory) => Instance = factory;
    }
}
