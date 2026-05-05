using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PlatformA.Library.Core;
using Polly.CircuitBreaker;
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
    /// RedisManager.ExecuteAsync를 통해 Polly 회로차단기가 적용됩니다.
    /// Redis 장애 시 fail-open(허용) 처리하여 서비스 가용성을 유지합니다.
    /// </summary>
    public class RedisRateLimiterService
    {
        private readonly RedisManager _redisManager;
        private readonly ILogger<RedisRateLimiterService> _logger;
        private readonly Dictionary<string, RateLimitPolicy> _policies = new();

        private const string _lua = @"
local now_ms    = tonumber(ARGV[3])
local window_ms = tonumber(ARGV[2]) * 1000
local limit     = tonumber(ARGV[1])
redis.call('ZREMRANGEBYSCORE', KEYS[1], 0, now_ms - window_ms)
local count = redis.call('ZCARD', KEYS[1])
if count >= limit then return 0 end
local t = redis.call('TIME')
local unique_id = t[1] .. t[2]
redis.call('ZADD', KEYS[1], now_ms, unique_id)
redis.call('EXPIRE', KEYS[1], tonumber(ARGV[2]) + 1)
return 1";

        public RedisRateLimiterService(
            RedisManager redisManager,
            ILogger<RedisRateLimiterService>? logger = null)
        {
            _redisManager = redisManager;
            _logger = logger ?? NullLogger<RedisRateLimiterService>.Instance;
        }

        public void AddPolicy(string name, int permitLimit, TimeSpan window)
        {
            _policies[name] = new RateLimitPolicy { PermitLimit = permitLimit, Window = window };
        }

        /// <summary>
        /// 요청 허용 여부를 Redis에서 원자적으로 검사합니다.
        /// BrokenCircuitException(회로차단기 개방) 또는 기타 Redis 오류 시 fail-open 처리합니다.
        /// </summary>
        public async Task<bool> IsAllowedAsync(string policyName, string clientIp)
        {
            if (!_policies.TryGetValue(policyName, out var policy))
                return true;

            try
            {
                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string key = $"rl:{policyName}:{clientIp}";

                var result = (int)await _redisManager.ExecuteAsync(db =>
                    db.ScriptEvaluateAsync(
                        _lua,
                        new RedisKey[] { key },
                        new RedisValue[] { policy.PermitLimit, (long)policy.Window.TotalSeconds, nowMs }
                    ));

                return result == 1;
            }
            catch (BrokenCircuitException)
            {
                // 회로차단기 개방 중 — fail-open: 차단하지 않음
                _logger.LogWarning("[RateLimit] 회로차단기 개방 중 — rate limit 우회 (fail-open)");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RateLimit] Redis 오류 — rate limit 우회 (fail-open)");
                return true;
            }
        }
    }
}
