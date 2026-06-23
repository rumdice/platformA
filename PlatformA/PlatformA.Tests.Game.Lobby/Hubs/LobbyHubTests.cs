using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using PlatformA.Game.Lobby.Hubs;
using PlatformA.Game.Lobby.Services;

namespace PlatformA.Tests.Game.Lobby.Hubs
{
    public class LobbyHubTests
    {
        private LobbyHub CreateHub(LobbyPresenceService? presence = null)
        {
            presence ??= new LobbyPresenceService();
            var httpFactory = new Mock<IHttpClientFactory>();
            var logger = new Mock<ILogger<LobbyHub>>();
            return new LobbyHub(presence, httpFactory.Object, logger.Object);
        }

        private Mock<HubCallerContext> CreateMockContext(int userId, string connectionId = "conn1")
        {
            var mockContext = new Mock<HubCallerContext>();
            mockContext.Setup(c => c.ConnectionId).Returns(connectionId);

            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            mockContext.Setup(c => c.User).Returns(principal);

            var items = new Dictionary<object, object?>();
            mockContext.Setup(c => c.Items).Returns(items);

            return mockContext;
        }

        private Mock<HubCallerContext> CreateMockContextWithNoClaims(string connectionId = "conn-no-claim")
        {
            var mockContext = new Mock<HubCallerContext>();
            mockContext.Setup(c => c.ConnectionId).Returns(connectionId);

            var identity = new ClaimsIdentity();
            var principal = new ClaimsPrincipal(identity);
            mockContext.Setup(c => c.User).Returns(principal);

            var items = new Dictionary<object, object?>();
            mockContext.Setup(c => c.Items).Returns(items);

            return mockContext;
        }

        [Fact]
        public async Task OnConnectedAsync_ValidUserId_RegistersInPresenceService()
        {
            var presence = new LobbyPresenceService();
            var hub = CreateHub(presence);
            var mockContext = CreateMockContext(userId: 42, connectionId: "conn-42");
            var mockClients = new Mock<IHubCallerClients>();
            hub.Context = mockContext.Object;
            hub.Clients = mockClients.Object;

            await hub.OnConnectedAsync();

            Assert.True(presence.IsOnline(42));
            Assert.Equal(42, mockContext.Object.Items["UserId"]);
        }

        [Fact]
        public async Task OnConnectedAsync_InvalidToken_AbortsConnection()
        {
            var presence = new LobbyPresenceService();
            var hub = CreateHub(presence);
            var mockContext = CreateMockContextWithNoClaims("conn-no-claim");
            var mockClients = new Mock<IHubCallerClients>();
            hub.Context = mockContext.Object;
            hub.Clients = mockClients.Object;

            await hub.OnConnectedAsync();

            mockContext.Verify(c => c.Abort(), Times.Once);
            Assert.Equal(0, presence.OnlineCount);
        }

        [Fact]
        public async Task OnConnectedAsync_UserIdZero_AbortsConnection()
        {
            var presence = new LobbyPresenceService();
            var hub = CreateHub(presence);
            var mockContext = CreateMockContext(userId: 0, connectionId: "conn-zero");
            var mockClients = new Mock<IHubCallerClients>();
            hub.Context = mockContext.Object;
            hub.Clients = mockClients.Object;

            await hub.OnConnectedAsync();

            mockContext.Verify(c => c.Abort(), Times.Once);
            Assert.False(presence.IsOnline(0));
        }

        [Fact]
        public async Task OnDisconnectedAsync_RegisteredUser_UnregistersFromPresence()
        {
            var presence = new LobbyPresenceService();
            presence.Register(42, "conn-42");

            var hub = CreateHub(presence);
            var mockContext = new Mock<HubCallerContext>();
            mockContext.Setup(c => c.ConnectionId).Returns("conn-42");

            // Items에 UserId=42 미리 설정
            var items = new Dictionary<object, object?> { ["UserId"] = 42 };
            mockContext.Setup(c => c.Items).Returns(items);

            var mockClients = new Mock<IHubCallerClients>();
            hub.Context = mockContext.Object;
            hub.Clients = mockClients.Object;

            await hub.OnDisconnectedAsync(null);

            Assert.False(presence.IsOnline(42));
        }

        [Fact]
        public async Task OnDisconnectedAsync_NoUserId_DoesNotThrow()
        {
            var presence = new LobbyPresenceService();
            var hub = CreateHub(presence);

            var mockContext = new Mock<HubCallerContext>();
            mockContext.Setup(c => c.ConnectionId).Returns("conn-no-user");

            // Items에 UserId 없음
            var items = new Dictionary<object, object?>();
            mockContext.Setup(c => c.Items).Returns(items);

            var mockClients = new Mock<IHubCallerClients>();
            hub.Context = mockContext.Object;
            hub.Clients = mockClients.Object;

            var ex = await Record.ExceptionAsync(() => hub.OnDisconnectedAsync(null));

            Assert.Null(ex);
        }
    }
}
