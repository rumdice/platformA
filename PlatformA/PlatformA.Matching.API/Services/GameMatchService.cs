using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Matching.API.Hubs;
using PlatformA.MySqlDB.Lib.DBWebApp;
using PlatformA.MySqlDB.Lib.DBWebApp.Entities;
using Polly.CircuitBreaker;
using StackExchange.Redis;

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

        // Lua: 타임아웃된 유저를 원자적으로 제거하고 반환
        private const string TIMEOUT_CLEANUP_SCRIPT = @"
local timedOut = redis.call('ZRANGEBYSCORE', KEYS[1], '-inf', ARGV[1])
if #timedOut > 0 then
    redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', ARGV[1])
end
return timedOut";

        // Lua: 가장 오래 기다린 유저 2명을 원자적으로 pop
        // ZPOPMIN은 [member, score, member, score, ...] 형식으로 반환
        private const string POP_TWO_SCRIPT = @"
local members = redis.call('ZPOPMIN', KEYS[1], 2)
if #members < 4 then
    if #members == 2 then
        redis.call('ZADD', KEYS[1], members[2], members[1])
    end
    return {}
end
return {members[1], members[3]}";

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

        /// <summary>매칭 큐에 유저를 추가합니다. (Sorted Set, score = 입장 시각 UnixMs)</summary>
        public async Task AddPlayerToQueueAsync(int userId)
        {
            _logger.LogInformation("[Matching] 유저 큐 진입 — UserId: {UserId}", userId);
            double score = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _redisManager.ExecuteAsync(db =>
                db.SortedSetAddAsync(Consts.MATCH_QUEUE_KEY, userId, score));
            _logger.LogInformation("[Matching] Redis 대기열 진입 완료 — UserId: {UserId}", userId);
        }

        /// <summary>매칭 대기열에서 유저를 제거합니다. true이면 실제로 제거됨.</summary>
        public async Task<bool> RemovePlayerFromQueueAsync(int userId)
        {
            long removed = await _redisManager.ExecuteAsync(db =>
                db.SortedSetRemoveAsync(Consts.MATCH_QUEUE_KEY, userId));
            return removed > 0;
        }

        /// <summary>유저의 대기열 순위를 반환합니다. 없으면 -1.</summary>
        public async Task<long> GetQueueRankAsync(int userId)
        {
            long? rank = await _redisManager.ExecuteAsync(db =>
                db.SortedSetRankAsync(Consts.MATCH_QUEUE_KEY, userId));
            return rank ?? -1;
        }

        /// <summary>현재 대기열 총 인원을 반환합니다.</summary>
        public async Task<long> GetQueueLengthAsync()
        {
            return await _redisManager.ExecuteAsync(db =>
                db.SortedSetLengthAsync(Consts.MATCH_QUEUE_KEY));
        }

        /// <summary>백그라운드 매칭 워커</summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessQueueAsync();
                    await Task.Delay(200, stoppingToken);
                }
                catch (BrokenCircuitException)
                {
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

        private async Task ProcessQueueAsync()
        {
            // 1. 타임아웃 유저 정리 (MATCH_TIMEOUT_SECONDS 초 초과 대기 유저)
            double cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                - (Consts.MATCH_TIMEOUT_SECONDS * 1000L);

            var timeoutResult = (RedisValue[])await _redisManager.ExecuteAsync(db =>
                db.ScriptEvaluateAsync(
                    TIMEOUT_CLEANUP_SCRIPT,
                    new RedisKey[] { Consts.MATCH_QUEUE_KEY },
                    new RedisValue[] { cutoff }));

            foreach (var val in timeoutResult)
            {
                if (int.TryParse(val, out int timedOutId))
                {
                    _logger.LogInformation("[Matching] 매칭 타임아웃 — UserId: {UserId}", timedOutId);
                    await _hubContext.Clients
                        .Group($"User_{timedOutId}")
                        .SendAsync("MatchTimeout", new { Message = "매칭 시간이 초과되었습니다." });
                }
            }

            // 2. 유저 2명 원자 pop (Lua로 race condition 방지)
            var popResult = (RedisValue[])await _redisManager.ExecuteAsync(db =>
                db.ScriptEvaluateAsync(
                    POP_TWO_SCRIPT,
                    new RedisKey[] { Consts.MATCH_QUEUE_KEY }));

            if (popResult.Length < 2)
                return;

            if (int.TryParse(popResult[0], out int player1Id) &&
                int.TryParse(popResult[1], out int player2Id))
            {
                _ = ProcessMatchingAsync(player1Id, player2Id);
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

        private async Task RecordMatchStartAsync(int player1Id, int player2Id)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                var record = new MatchRecord
                {
                    Player1Id = player1Id,
                    Player2Id = player2Id,
                    Status = MatchStatus.InProgress,
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
