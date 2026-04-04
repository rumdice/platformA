using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Polly.CircuitBreaker;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Matching.API.Hubs;
using PlatformA.MySqlDB.Lib.DBWebApp;
using PlatformA.MySqlDB.Lib.DBWebApp.Entities;
using StackExchange.Redis;
using System.Text.Json;

namespace PlatformA.Matching.API.Services
{
    /// <summary>
    /// 게임서버용 매칭 엔진.
    /// 모든 Redis 명령은 RedisManager.ExecuteAsync를 통해 Polly 파이프라인이 적용됩니다.
    /// </summary>
    public class GameMatchService : BackgroundService
    {
        private readonly IHubContext<MatchingHub> _hubContext;
        private readonly RedisManager _redisManager;
        private readonly ILogger<GameMatchService> _logger;
        private readonly IDbContextFactory<DbWebAppContext> _dbFactory;
        private const string MATCH_QUEUE_KEY = "queue:gamematch:1v1";

        public GameMatchService(
            IHubContext<MatchingHub> hubContext,
            RedisManager redisManager,
            ILogger<GameMatchService> logger,
            IDbContextFactory<DbWebAppContext> dbFactory)
        {
            _hubContext = hubContext;
            _redisManager = redisManager;
            _logger = logger;
            _dbFactory = dbFactory;
        }

        /// <summary>매칭 큐에 유저를 추가합니다.</summary>
        public async Task AddPlayerToQueueAsync(int userId)
        {
            _logger.LogInformation("[Matching] 유저 큐 진입 — UserId: {UserId}", userId);
            await _redisManager.ExecuteAsync(db => db.ListRightPushAsync(MATCH_QUEUE_KEY, userId));
            _logger.LogInformation("[Matching] Redis 대기열 진입 완료 — UserId: {UserId}", userId);
        }

        /// <summary>백그라운드 매칭 워커</summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    long queueLength = await _redisManager.ExecuteAsync(
                        db => db.ListLengthAsync(MATCH_QUEUE_KEY));

                    if (queueLength >= 2)
                    {
                        var user1Val = await _redisManager.ExecuteAsync(
                            db => db.ListLeftPopAsync(MATCH_QUEUE_KEY));
                        var user2Val = await _redisManager.ExecuteAsync(
                            db => db.ListLeftPopAsync(MATCH_QUEUE_KEY));

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
                catch (BrokenCircuitException)
                {
                    // 회로차단기 OPEN — Redis 회복 대기 (BreakDuration 60s)
                    _logger.LogWarning("[Matching] 회로차단기 OPEN — Redis 회복 대기 중 (5초 후 재시도)");
                    await Task.Delay(5000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
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
            int newRoomId = (int)await _redisManager.ExecuteAsync(
                db => db.StringIncrementAsync("global:room_id"));

            // room_id 1번은 서버 시작 시 생성된 광장(로비)이므로 건너뜁니다.
            if (newRoomId == 1)
                newRoomId = (int)await _redisManager.ExecuteAsync(
                    db => db.StringIncrementAsync("global:room_id"));

            _logger.LogInformation("[Matching] 매칭 성사 — 방: {RoomId}, Player1: {P1}, Player2: {P2}",
                newRoomId, player1Id, player2Id);

            // ── MatchRecord DB 기록 ────────────────────────────────
            await RecordMatchStartAsync(player1Id, player2Id);

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

        /// <summary>
        /// 매칭 성사 시 match_records 테이블에 InProgress 상태로 기록합니다.
        /// 실패해도 매칭 흐름을 중단하지 않습니다.
        /// </summary>
        private async Task RecordMatchStartAsync(int player1Id, int player2Id)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                var record = new MatchRecord
                {
                    Player1Id = player1Id,
                    Player2Id = player2Id,
                    Status    = MatchStatus.InProgress,
                    StartedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };
                db.MatchRecords.Add(record);
                await db.SaveChangesAsync();

                _logger.LogInformation(
                    "[Matching] MatchRecord 생성 완료 — RecordId: {Id}, P1: {P1}, P2: {P2}",
                    record.Id, player1Id, player2Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[Matching] MatchRecord 저장 실패 — P1: {P1}, P2: {P2} (매칭 흐름 계속)",
                    player1Id, player2Id);
            }
        }
    }
}
