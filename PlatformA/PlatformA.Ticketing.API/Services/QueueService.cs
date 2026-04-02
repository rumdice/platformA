using PlatformA.Library.Common;
using PlatformA.Library.Core;
using StackExchange.Redis;

namespace PlatformA.Ticketing.API.Services
{
    /// <summary>
    /// 게임 입장 대기열 위한 서비스
    /// </summary>
    public class QueueService
    {
        private readonly RedisManager _redisManager;

        public QueueService(RedisManager redisManager)
        {
            _redisManager = redisManager;
        }

        /// <summary>
        /// 대기열 진입(등록). 줄서기.
        /// ZCARD 체크 + ZADD를 Lua 스크립트로 원자화하여 Race Condition을 제거합니다.
        /// </summary>
        /// <returns>true: 등록 성공, false: 대기열 초과 또는 이미 등록됨</returns>
        public async Task<bool> RegisterQueueAsync(int userId)
        {
            var db = _redisManager.Connection.GetDatabase();
            double score = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // [핵심] ZCARD 체크 → ZADD를 두 번의 Redis 호출로 나누면
            // 동시 요청 시 둘 다 체크를 통과해 MAX_SIZE를 초과할 수 있습니다.
            // Lua 스크립트로 원자적으로 처리합니다.
            var script = @"
                local size = redis.call('ZCARD', KEYS[1])
                if size >= tonumber(ARGV[3]) then return -1 end
                local added = redis.call('ZADD', KEYS[1], 'NX', ARGV[2], ARGV[1])
                return added";

            // Returns: -1 = 대기열 초과, 1 = 신규 등록, 0 = 이미 등록된 유저
            var result = (int)await db.ScriptEvaluateAsync(
                script,
                new RedisKey[] { Consts.QUEUE_KEY },
                new RedisValue[] { userId.ToString(), score, Consts.WAIT_QUEUE_MAX_SIZE }
            );

            if (result == -1)
            {
                Console.WriteLine($"[대기열 초과] 유저 {userId} 진입 거절 (현재 큐 {Consts.WAIT_QUEUE_MAX_SIZE}명 도달)");
                return false;
            }

            if (result == 1)
                Console.WriteLine($"[대기열 진입] 유저 {userId} 등록 완료 (Score: {score})");

            return result == 1;
        }


        /// <summary>
        /// 대기 순번 조회 (ZRANK) : 내 순번 및 입장 가능 여부 확인
        /// </summary>
        public async Task<long?> GetRankAsync(int userId)
        {
            var db = _redisManager.Connection.GetDatabase();

            // ZRANK: Score가 가장 낮은(먼저 온) 사람부터 0번 인덱스를 부여
            long? rankIndex = await db.SortedSetRankAsync(Consts.QUEUE_KEY, userId);

            // 클라이언트에게 보여줄 때는 1등부터 보여주는 것이 자연스러우므로 +1 처리
            if (rankIndex.HasValue)
                return rankIndex.Value + 1;

            return null;
        }


        /// <summary>
        /// 대기열 명시적 이탈.
        /// 대기열(QUEUE_KEY) + 하트비트를 Lua로 원자적 동시 제거합니다.
        /// </summary>
        /// <returns>true: 대기열에 있었고 제거됨, false: 대기열에 없었음</returns>
        public async Task<bool> LeaveQueueAsync(int userId)
        {
            var db = _redisManager.Connection.GetDatabase();

            // ZREM은 제거된 원소 수(0 또는 1)를 반환합니다.
            // 두 ZSET을 한 번의 Lua 호출로 원자적으로 처리합니다.
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
                Console.WriteLine($"[대기열 이탈] 유저 {userId} 명시적 이탈 완료");

            return result == 1;
        }


        /// <summary>
        /// 입장 가능한 유저인지 판별.
        /// 개별 Redis 키(TTL 포함)로 관리하므로 시간이 지나면 자동 만료됩니다.
        /// </summary>
        public async Task<bool> IsActiveAsync(int userId)
        {
            var db = _redisManager.Connection.GetDatabase();
            return await db.KeyExistsAsync($"{Consts.ACTIVE_USER_KEY_PREFIX}{userId}");
        }


        public async Task UpdateHeartbeatAsync(int userId)
        {
            var db = _redisManager.Connection.GetDatabase();
            double currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // 하트비트 전용 ZSET에 생존 시간 갱신
            await db.SortedSetAddAsync("ticket:queue:heartbeats", userId, currentTimestamp);
        }
    }
}
