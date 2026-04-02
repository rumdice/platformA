using PlatformA.Library.Core;
using StackExchange.Redis;

namespace PlatformA.Library.RateLimit
{
    public class RateLimitPolicy
    {
        public int PermitLimit { get; init; }
        public TimeSpan Window { get; init; }
    }

    /// <summary>
    /// Redis 기반 분산 Sliding-Window Rate Limiter.
    ///
    /// [Fixed-Window 대비 개선점]
    /// Fixed-Window는 창 경계(예: 분 0:59 → 분 1:00)에서 limit의 2배 요청이
    /// 순간적으로 허용되는 burst 취약점이 있습니다.
    /// Sliding-Window는 "현재 시각 기준 과거 N초 이내의 실제 요청 수"를
    /// ZSET으로 정확히 계산하므로 경계 burst가 발생하지 않습니다.
    ///
    /// [구조]
    /// Key    : rl:{policyName}:{clientIp}  (ZSET, 버킷 없음)
    /// Member : Redis TIME 기반 마이크로초 고유 ID
    /// Score  : 요청 시각 (milliseconds)
    /// </summary>
    public class RedisRateLimiterService
    {
        private readonly IDatabase? _db;
        private readonly Dictionary<string, RateLimitPolicy> _policies = new();

        // Sliding-Window Lua 스크립트
        // KEYS[1] = rate limit ZSET 키
        // ARGV[1] = permit limit
        // ARGV[2] = window 초 (seconds)
        // ARGV[3] = 현재 시각 (milliseconds, 애플리케이션에서 전달)
        // Returns: 1 = 허용, 0 = 거절
        private const string _lua = @"
local now_ms    = tonumber(ARGV[3])
local window_ms = tonumber(ARGV[2]) * 1000
local limit     = tonumber(ARGV[1])

-- 1. 슬라이딩 윈도우 밖 만료 항목 제거
redis.call('ZREMRANGEBYSCORE', KEYS[1], 0, now_ms - window_ms)

-- 2. 현재 윈도우 내 요청 수 확인
local count = redis.call('ZCARD', KEYS[1])
if count >= limit then
    return 0
end

-- 3. 현재 요청 기록
--    Redis TIME 명령으로 마이크로초 고유 ID 생성 → 동일 밀리초에 여러 요청이 와도 중복 없음
local t = redis.call('TIME')
local unique_id = t[1] .. t[2]
redis.call('ZADD', KEYS[1], now_ms, unique_id)

-- 4. 키 TTL 갱신 (윈도우 경과 후 자동 정리)
redis.call('EXPIRE', KEYS[1], tonumber(ARGV[2]) + 1)

return 1";

        public RedisRateLimiterService(RedisManager redisManager)
        {
            _db = redisManager.Connection?.GetDatabase();
        }

        public void AddPolicy(string name, int permitLimit, TimeSpan window)
        {
            _policies[name] = new RateLimitPolicy { PermitLimit = permitLimit, Window = window };
        }

        /// <summary>
        /// 요청을 허용할지 여부를 Redis에서 원자적으로 검사합니다.
        /// Redis 장애 시에는 fail-open (허용) 처리합니다.
        /// </summary>
        public async Task<bool> IsAllowedAsync(string policyName, string clientIp)
        {
            if (_db == null) return true;

            if (!_policies.TryGetValue(policyName, out var policy))
                return true;

            try
            {
                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // Fixed-Window와 달리 버킷 분할 없음 — 단일 키로 슬라이딩 윈도우 유지
                string key = $"rl:{policyName}:{clientIp}";

                var result = (int)await _db.ScriptEvaluateAsync(
                    _lua,
                    new RedisKey[] { key },
                    new RedisValue[] { policy.PermitLimit, (long)policy.Window.TotalSeconds, nowMs }
                );

                return result == 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RateLimit] Redis 오류, rate limit 우회: {ex.Message}");
                return true;
            }
        }
    }
}
