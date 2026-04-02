using Microsoft.AspNetCore.SignalR;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Matching.API.Hubs;
using StackExchange.Redis;
using System.Text.Json;

namespace PlatformA.Matching.API.Services
{
    /// <summary>
    /// 게임서버용 매칭 엔진.
    /// </summary>
    public class GameMatchService : BackgroundService
    {
        private readonly IHubContext<MatchingHub> _hubContext;
        private readonly RedisManager _redisManager;
        private readonly ILogger<GameMatchService> _logger;
        private const string MATCH_QUEUE_KEY = "queue:gamematch:1v1";

        public GameMatchService(
            IHubContext<MatchingHub> hubContext,
            RedisManager redisManager,
            ILogger<GameMatchService> logger)
        {
            _hubContext = hubContext;
            _redisManager = redisManager;
            _logger = logger;
        }

        /// <summary>매칭 큐에 유저를 추가합니다.</summary>
        public async Task AddPlayerToQueueAsync(int userId)
        {
            _logger.LogInformation("[Matching] 유저 큐 진입 — UserId: {UserId}", userId);
            var db = _redisManager.Connection.GetDatabase();
            await db.ListRightPushAsync(MATCH_QUEUE_KEY, userId);
            _logger.LogInformation("[Matching] Redis 대기열 진입 완료 — UserId: {UserId}", userId);
        }

        /// <summary>백그라운드 매칭 워커</summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var db = _redisManager.Connection.GetDatabase();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    long queueLength = await db.ListLengthAsync(MATCH_QUEUE_KEY);

                    if (queueLength >= 2)
                    {
                        var user1Val = await db.ListLeftPopAsync(MATCH_QUEUE_KEY);
                        var user2Val = await db.ListLeftPopAsync(MATCH_QUEUE_KEY);

                        if (user1Val.HasValue && user2Val.HasValue)
                        {
                            int player1Id = (int)user1Val;
                            int player2Id = (int)user2Val;
                            _ = ProcessMatchingAsync(player1Id, player2Id);
                        }
                        else
                        {
                            await Task.Delay(1000, stoppingToken);
                        }
                    }
                    else
                    {
                        await Task.Delay(1000, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Matching] 매칭 처리 중 예외 발생");
                    await Task.Delay(2000, stoppingToken);
                }
            }
        }

        private async Task ProcessMatchingAsync(int player1Id, int player2Id)
        {
            var db = _redisManager.Connection.GetDatabase();
            int newRoomId = (int)await db.StringIncrementAsync("global:room_id");

            if (newRoomId == 1)
                newRoomId = (int)await db.StringIncrementAsync("global:room_id");

            _logger.LogInformation("[Matching] 매칭 성사 — 방: {RoomId}, Player1: {P1}, Player2: {P2}",
                newRoomId, player1Id, player2Id);

            var matchEvent = new MatchSuccessEvent
            {
                RoomId = newRoomId,
                MatchedUserIds = new List<int> { player1Id, player2Id }
            };

            string jsonMessage = JsonSerializer.Serialize(matchEvent);

            await _redisManager.GetSubscriber().PublishAsync(
                RedisChannel.Literal("channel:match_success"), jsonMessage);

            await _hubContext.Clients.Group($"User_{player1Id}").SendAsync("MatchFound", matchEvent);
            await _hubContext.Clients.Group($"User_{player2Id}").SendAsync("MatchFound", matchEvent);
        }
    }
}
