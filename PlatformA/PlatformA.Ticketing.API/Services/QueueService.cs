using Polly.CircuitBreaker;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using StackExchange.Redis;

namespace PlatformA.Ticketing.API.Services
{
    /// <summary>
    /// 게임 입장 대기열 서비스.
    /// 모든 Redis 명령은 RedisManager.ExecuteAsync를 통해 Polly 파이프라인이 적용됩니다.
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
        /// 대기열 진입. ZCARD 체크 + ZADD를 Lua로 원자화 (Race Condition 제거).
        /// QUEUE_KEY / QUEUE_HEARTBEATS_KEY 는 동일 해시태그 {ticket:queue} → 같은 클러스터 슬롯.
        /// </summary>
        public async Task<bool> RegisterQueueAsync(int userId)
        {
            double score = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var script = @"
                local size = redis.call('ZCARD', KEYS[1])
                if size >= tonumber(ARGV[3]) then return -1 end
                local added = redis.call('ZADD', KEYS[1], 'NX', ARGV[2], ARGV[1])
                return added";

            try
            {
                var result = (int)await _redisManager.ExecuteAsync(db =>
                    db.ScriptEvaluateAsync(
                        script,
                        new RedisKey[] { Consts.QUEUE_KEY },
                        new RedisValue[] { userId.ToString(), score, Consts.WAIT_QUEUE_MAX_SIZE }
                    ));

                if (result == -1)
                {
                    _logger.LogWarning("[Queue] 대기열 초과 — UserId: {UserId}, 최대: {Max}", userId, Consts.WAIT_QUEUE_MAX_SIZE);
                    return false;
                }

                if (result == 1)
                {
                    _logger.LogInformation("[Queue] 대기열 진입 — UserId: {UserId}", userId);
                }
                else // result == 0
                {
                    _logger.LogInformation("[Queue] 이미 대기열에 있음 — UserId: {UserId}", userId);
                }

                return true;
            }
            catch (BrokenCircuitException)
            {
                _logger.LogError("[Queue] 회로차단기 개방 — 대기열 진입 불가 (Redis 장애)");
                throw; // 컨트롤러가 503으로 처리
            }
        }

        /// <summary>대기 순번 조회 (1-based)</summary>
        public async Task<long?> GetRankAsync(int userId)
        {
            long? rankIndex = await _redisManager.ExecuteAsync(db =>
                db.SortedSetRankAsync(Consts.QUEUE_KEY, userId));
            return rankIndex.HasValue ? rankIndex.Value + 1 : null;
        }

        /// <summary>
        /// 명시적 이탈. QUEUE_KEY + QUEUE_HEARTBEATS_KEY 를 Lua로 원자 제거.
        /// 두 키 모두 {ticket:queue} 해시태그 → 같은 슬롯 보장.
        /// </summary>
        public async Task<bool> LeaveQueueAsync(int userId)
        {
            var script = @"
local removed = redis.call('ZREM', KEYS[1], ARGV[1])
redis.call('ZREM', KEYS[2], ARGV[1])
return removed";

            var result = (int)await _redisManager.ExecuteAsync(db =>
                db.ScriptEvaluateAsync(
                    script,
                    new RedisKey[] { Consts.QUEUE_KEY, Consts.QUEUE_HEARTBEATS_KEY },
                    new RedisValue[] { userId.ToString() }
                ));

            if (result == 1)
                _logger.LogInformation("[Queue] 명시적 이탈 — UserId: {UserId}", userId);

            return result == 1;
        }

        /// <summary>Active 유저 여부 확인 (개별 키 TTL 방식)</summary>
        public async Task<bool> IsActiveAsync(int userId)
        {
            return await _redisManager.ExecuteAsync(db =>
                db.KeyExistsAsync($"{Consts.ACTIVE_USER_KEY_PREFIX}{userId}"));
        }

        public async Task UpdateHeartbeatAsync(int userId)
        {
            double ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _redisManager.ExecuteAsync(db =>
                db.SortedSetAddAsync(Consts.QUEUE_HEARTBEATS_KEY, userId, ts));
        }


        public async Task<long?> UpdateHeartbeatAndGetRankAsync(int userId)
        {
            double ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var script = @"
redis.call('ZADD', KEYS[1],tonumber(ARGV[2]), ARGV[1])
local rank = redis.call('ZRANK', KEYS[2],ARGV[1])
if rank == false then return -1 end
return rank + 1";

            var result = (long)await _redisManager.ExecuteAsync(db =>
            db.ScriptEvaluateAsync(
                script,
                new RedisKey[]{ 
                    Consts.QUEUE_HEARTBEATS_KEY,
                    Consts.QUEUE_KEY 
                }, 
                new RedisValue[] { 
                    userId.ToString(),
                    ts 
                }));

            return result == -1 ? null : result;
        }
    }
}
