using PlatformA.Library.Common;
using PlatformA.Library.Core;
using StackExchange.Redis;

namespace PlatformA.Ticketing.API.Services
{
    /// <summary>
    /// 게임 입장 대기열 서비스
    /// </summary>
    public class QueueService
    {
        private readonly RedisManager _redisManager;
        private readonly ILogger<QueueService> _logger;

        public QueueService(RedisManager redisManager, ILogger<QueueService> logger)
        {
            _redisManager = redisManager;
            _logger = logger;
        }

        /// <summary>
        /// 대기열 진입(등록). ZCARD 체크 + ZADD를 Lua로 원자화하여 Race Condition 제거.
        /// </summary>
        /// <returns>true: 등록 성공, false: 대기열 초과 또는 이미 등록됨</returns>
        public async Task<bool> RegisterQueueAsync(int userId)
        {
            var db = _redisManager.Connection.GetDatabase();
            double score = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var script = @"
                local size = redis.call('ZCARD', KEYS[1])
                if size >= tonumber(ARGV[3]) then return -1 end
                local added = redis.call('ZADD', KEYS[1], 'NX', ARGV[2], ARGV[1])
                return added";

            var result = (int)await db.ScriptEvaluateAsync(
                script,
                new RedisKey[] { Consts.QUEUE_KEY },
                new RedisValue[] { userId.ToString(), score, Consts.WAIT_QUEUE_MAX_SIZE }
            );

            if (result == -1)
            {
                _logger.LogWarning("[Queue] 대기열 초과 — UserId: {UserId}, 최대: {Max}", userId, Consts.WAIT_QUEUE_MAX_SIZE);
                return false;
            }

            if (result == 1)
                _logger.LogInformation("[Queue] 대기열 진입 — UserId: {UserId}, Score: {Score}", userId, score);

            return result == 1;
        }

        /// <summary>대기 순번 조회 (1-based)</summary>
        public async Task<long?> GetRankAsync(int userId)
        {
            var db = _redisManager.Connection.GetDatabase();
            long? rankIndex = await db.SortedSetRankAsync(Consts.QUEUE_KEY, userId);
            return rankIndex.HasValue ? rankIndex.Value + 1 : null;
        }

        /// <summary>
        /// 대기열 명시적 이탈. QUEUE_KEY + heartbeats를 Lua로 원자적 동시 제거.
        /// </summary>
        public async Task<bool> LeaveQueueAsync(int userId)
        {
            var db = _redisManager.Connection.GetDatabase();
            var script = @"
local removed = redis.call('ZREM', KEYS[1], ARGV[1])
redis.call('ZREM', KEYS[2], ARGV[1])
return removed";

            var result = (int)await db.ScriptEvaluateAsync(
                script,
                new RedisKey[] { Consts.QUEUE_KEY, "ticket:queue:heartbeats" },
                new RedisValue[] { userId.ToString() }
            );

            if (result == 1)
                _logger.LogInformation("[Queue] 명시적 이탈 — UserId: {UserId}", userId);

            return result == 1;
        }

        /// <summary>Active 유저 여부 확인 (개별 키 TTL 방식)</summary>
        public async Task<bool> IsActiveAsync(int userId)
        {
            var db = _redisManager.Connection.GetDatabase();
            return await db.KeyExistsAsync($"{Consts.ACTIVE_USER_KEY_PREFIX}{userId}");
        }

        public async Task UpdateHeartbeatAsync(int userId)
        {
            var db = _redisManager.Connection.GetDatabase();
            double currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await db.SortedSetAddAsync("ticket:queue:heartbeats", userId, currentTimestamp);
        }
    }
}
