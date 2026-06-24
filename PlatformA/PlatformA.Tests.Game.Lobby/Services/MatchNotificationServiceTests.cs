using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using PlatformA.Game.Lobby.Hubs;
using PlatformA.Game.Lobby.Services;
using StackExchange.Redis;

namespace PlatformA.Tests.Game.Lobby.Services
{
    public class MatchNotificationServiceTests
    {
        private readonly Mock<IHubContext<LobbyHub>> _mockHubContext;
        private readonly Mock<IHubClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;
        private readonly Mock<ILogger<MatchNotificationService>> _mockLogger;
        private readonly MatchNotificationService _service;

        public MatchNotificationServiceTests()
        {
            _mockClientProxy = new Mock<IClientProxy>();
            _mockClients = new Mock<IHubClients>();
            _mockClients
                .Setup(c => c.User(It.IsAny<string>()))
                .Returns(_mockClientProxy.Object);

            _mockHubContext = new Mock<IHubContext<LobbyHub>>();
            _mockHubContext
                .Setup(h => h.Clients)
                .Returns(_mockClients.Object);

            _mockLogger = new Mock<ILogger<MatchNotificationService>>();

            _service = new MatchNotificationService(
                _mockHubContext.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task ProcessMatchFoundAsync_ValidPayload_SendsMatchFoundToCorrectUser()
        {
            // Arrange
            const string payload =
                "{\"userId\":1,\"host\":\"127.0.0.1\",\"port\":7778,\"roomId\":\"abc123\",\"gameType\":\"gomoku\"}";

            // Act
            await _service.ProcessMatchFoundAsync((RedisValue)payload);

            // Assert — Clients.User("1")으로 MatchFound 전송
            _mockClients.Verify(c => c.User("1"), Times.Once);
            _mockClientProxy.Verify(
                p => p.SendCoreAsync(
                    "MatchFound",
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ProcessMatchFoundAsync_MissingGameType_StillSendsWithEmptyGameType()
        {
            // Arrange — gameType 필드 없음
            const string payload =
                "{\"userId\":2,\"host\":\"127.0.0.1\",\"port\":7778,\"roomId\":\"abc456\"}";

            // Act
            await _service.ProcessMatchFoundAsync((RedisValue)payload);

            // Assert — 예외 없이 SendAsync 정상 호출
            _mockClients.Verify(c => c.User("2"), Times.Once);
            _mockClientProxy.Verify(
                p => p.SendCoreAsync(
                    "MatchFound",
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ProcessMatchFoundAsync_InvalidJson_LogsError_DoesNotThrow()
        {
            // Arrange
            const string badPayload = "not-json";

            // Act — 예외가 발생하면 안 됨
            Exception? ex = await Record.ExceptionAsync(
                () => _service.ProcessMatchFoundAsync((RedisValue)badPayload));

            Assert.Null(ex);

            // Assert — LogError 호출 확인
            _mockLogger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            // SendAsync는 호출되지 않아야 함
            _mockClientProxy.Verify(
                p => p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}
