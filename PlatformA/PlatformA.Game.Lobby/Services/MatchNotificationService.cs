using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using PlatformA.Game.Lobby.Hubs;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using StackExchange.Redis;

namespace PlatformA.Game.Lobby.Services
{
    /// <summary>
    /// Redis channel:match_found 를 구독하여 매칭 성사 알림을 SignalR로 전달합니다.
    /// </summary>
    public class MatchNotificationService : BackgroundService
    {
        private readonly IHubContext<LobbyHub> _hubContext;
        private readonly ILogger<MatchNotificationService> _logger;
        private ISubscriber? _subscriber;

        public MatchNotificationService(
            IHubContext<LobbyHub> hubContext,
            ILogger<MatchNotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // RedisManager가 초기화될 때까지 대기
            while (RedisManager.Instance.Connection == null && !stoppingToken.IsCancellationRequested)
                await Task.Delay(500, stoppingToken);

            if (stoppingToken.IsCancellationRequested)
                return;

            _subscriber = RedisManager.Instance.GetSubscriber();
            await _subscriber.SubscribeAsync(
                RedisChannel.Literal(Consts.MATCH_FOUND_CHANNEL),
                OnMatchFound);

            _logger.LogInformation("[MatchNotification] Redis 구독 시작: {Channel}", Consts.MATCH_FOUND_CHANNEL);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private void OnMatchFound(RedisChannel channel, RedisValue message)
        {
            _ = ProcessMatchFoundAsync(message);
        }

        internal async Task ProcessMatchFoundAsync(RedisValue message)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(message.ToString());
                JsonElement root = doc.RootElement;

                int userId = root.GetProperty("userId").GetInt32();
                string host = root.GetProperty("host").GetString() ?? string.Empty;
                int port = root.GetProperty("port").GetInt32();
                string roomId = root.GetProperty("roomId").GetString() ?? string.Empty;
                string gameType = root.TryGetProperty("gameType", out JsonElement gt)
                    ? gt.GetString() ?? string.Empty
                    : string.Empty;

                _logger.LogInformation(
                    "[MatchNotification] 매칭 성사 알림 → User_{UserId} room={RoomId}",
                    userId, roomId);

                await _hubContext.Clients.User(userId.ToString()).SendAsync("MatchFound", new
                {
                    host,
                    port,
                    roomId,
                    gameType,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MatchNotification] 메시지 처리 오류");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_subscriber != null)
            {
                await _subscriber.UnsubscribeAsync(RedisChannel.Literal(Consts.MATCH_FOUND_CHANNEL));
                _logger.LogInformation("[MatchNotification] Redis 구독 해제");
            }

            await base.StopAsync(cancellationToken);
        }
    }
}
