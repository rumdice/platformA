using PlatformA.Game.Lobby.Services;

namespace PlatformA.Tests.Game.Lobby.Core
{
    public class LobbyPresenceServiceTests
    {
        [Fact]
        public void Register_NewUser_UserIsOnline()
        {
            var svc = new LobbyPresenceService();

            svc.Register(1, "conn-1");

            Assert.True(svc.IsOnline(1));
        }

        [Fact]
        public void Register_ExistingUser_UpdatesConnectionId()
        {
            var svc = new LobbyPresenceService();
            svc.Register(1, "conn-old");

            svc.Register(1, "conn-new");

            Assert.Equal("conn-new", svc.GetConnectionId(1));
        }

        [Fact]
        public void Unregister_RegisteredUser_UserIsOffline()
        {
            var svc = new LobbyPresenceService();
            svc.Register(1, "conn-1");

            svc.Unregister(1);

            Assert.False(svc.IsOnline(1));
        }

        [Fact]
        public void Unregister_UnknownUser_DoesNotThrow()
        {
            var svc = new LobbyPresenceService();

            var ex = Record.Exception(() => svc.Unregister(999));

            Assert.Null(ex);
        }

        [Fact]
        public void GetConnectionId_RegisteredUser_ReturnsCorrectId()
        {
            var svc = new LobbyPresenceService();
            svc.Register(42, "conn-abc");

            string? result = svc.GetConnectionId(42);

            Assert.Equal("conn-abc", result);
        }

        [Fact]
        public void GetConnectionId_UnregisteredUser_ReturnsNull()
        {
            var svc = new LobbyPresenceService();

            string? result = svc.GetConnectionId(999);

            Assert.Null(result);
        }

        [Fact]
        public void IsOnline_RegisteredUser_ReturnsTrue()
        {
            var svc = new LobbyPresenceService();
            svc.Register(7, "conn-7");

            Assert.True(svc.IsOnline(7));
        }

        [Fact]
        public void IsOnline_UnregisteredUser_ReturnsFalse()
        {
            var svc = new LobbyPresenceService();

            Assert.False(svc.IsOnline(7));
        }

        [Fact]
        public void OnlineCount_MultipleUsers_ReturnsCorrectCount()
        {
            var svc = new LobbyPresenceService();
            svc.Register(1, "conn-1");
            svc.Register(2, "conn-2");
            svc.Register(3, "conn-3");

            Assert.Equal(3, svc.OnlineCount);
        }

        [Fact]
        public async Task Register_ConcurrentAccess_ThreadSafe()
        {
            var svc = new LobbyPresenceService();
            int threadCount = 10;
            var tasks = Enumerable.Range(1, threadCount)
                .Select(i => Task.Run(() => svc.Register(i, $"conn-{i}")))
                .ToArray();

            await Task.WhenAll(tasks);

            for (int i = 1; i <= threadCount; i++)
            {
                Assert.True(svc.IsOnline(i), $"User {i} should be online after concurrent Register");
            }
        }
    }
}
