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
    /// Redis 기반 분산 Fixed-Window Rate Limiter.
    /// 여러 인스턴스가 동일한 Redis를 바라보므로 수평 확장 시에도 정확한 제한이 적용됩니다.
    /// </summary>
    public class RedisRateLimiterService
    {
        private readonly IDatabase? _db;
        private readonly Dictionary<string, RateLimitPolicy> _policies = new();

        // Lua: INCR + EXPIRE 원자 실행 (Fixed-Window)
        // KEYS[1] = rate limit key
        // ARGV[1] = permit limit
        // ARGV[2] = window seconds
        // Returns: 1 = 허용, 0 = 거절
        private const string _lua = @"
local current = redis.call('INCR', KEYS[1])
if current == 1 then
    redis.call('EXPIRE', KEYS[1], ARGV[2])
end
if current > tonumber(ARGV[1]) then
    return 0
end
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
            if (_db == null) return true; // Redis 미연결 → fail-open

            if (!_policies.TryGetValue(policyName, out var policy))
                return true;

            try
            {
                long windowSeconds = (long)policy.Window.TotalSeconds;
                long bucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / windowSeconds;
                string key = $"rl:{policyName}:{clientIp}:{bucket}";

                var result = (int)await _db.ScriptEvaluateAsync(
                    _lua,
                    new RedisKey[] { key },
                    new RedisValue[] { policy.PermitLimit, windowSeconds }
                );

                return result == 1;
            }
            catch (Exception ex)
            {
                // Redis 장애 시 서비스 중단보다 rate limit 우회를 선택 (가용성 우선)
                Console.WriteLine($"[RateLimit] Redis 오류, rate limit 우회: {ex.Message}");
                return true;
            }
        }
    }
}
