using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using PlatformA.Library.Common;
using PlatformA.Library.Core;

namespace PlatformA.Auth.API.Services
{
    /// <summary>
    /// Redis 기반 Refresh Token 관리 서비스.
    /// RedisManager.ExecuteAsync를 통해 Polly 회로차단기 + 재시도가 적용됩니다.
    /// </summary>
    public class RefreshTokenService
    {
        private readonly RedisManager _redisManager;
        private readonly ILogger<RefreshTokenService> _logger;

        public RefreshTokenService(RedisManager redisManager, ILogger<RefreshTokenService> logger)
        {
            _redisManager = redisManager;
            _logger = logger;
        }

        public async Task SaveAsync(string refreshToken, int userId)
        {
            string key = Consts.REFRESH_TOKEN_KEY_PREFIX + refreshToken;
            await _redisManager.ExecuteAsync(db =>
                db.StringSetAsync(key, userId.ToString(),
                    TimeSpan.FromDays(Consts.REFRESH_TOKEN_EXPIRY_DAYS)));
        }

        public async Task<int?> GetUserIdAsync(string refreshToken)
        {
            string key = Consts.REFRESH_TOKEN_KEY_PREFIX + refreshToken;
            try
            {
                var value = await _redisManager.ExecuteAsync(db => db.StringGetAsync(key));
                if (value.IsNullOrEmpty) return null;
                return int.Parse(value!);
            }
            catch (BrokenCircuitException)
            {
                _logger.LogWarning("[RefreshToken] 회로차단기 개방 — 토큰 검증 불가");
                return null; // fail-closed: 인증 불가 처리
            }
        }

        /// <summary>
        /// 토큰 검증과 폐기를 원자적으로 수행합니다 (Lua GET+DEL).
        /// 동시 요청에서 동일 Refresh Token이 두 번 사용되는 경쟁 조건을 방지합니다.
        /// </summary>
        public async Task<int?> GetAndRevokeAsync(string refreshToken)
        {
            string key = Consts.REFRESH_TOKEN_KEY_PREFIX + refreshToken;
            // GET + DEL 을 하나의 Lua 트랜잭션으로 처리 — 두 스레드가 동시에 접근해도
            // 둘 중 하나만 값을 얻고, 나머지는 nil을 받습니다.
            const string script = @"
                local val = redis.call('get', KEYS[1])
                if val then
                    redis.call('del', KEYS[1])
                    return val
                end
                return nil";
            try
            {
                var result = await _redisManager.ExecuteAsync(db =>
                    db.ScriptEvaluateAsync(script, new StackExchange.Redis.RedisKey[] { key }));
                if (result.IsNull) return null;
                return int.Parse(result.ToString()!);
            }
            catch (BrokenCircuitException)
            {
                _logger.LogWarning("[RefreshToken] 회로차단기 개방 — 원자적 토큰 검증 불가");
                return null;
            }
        }

        public async Task RevokeAsync(string refreshToken)
        {
            string key = Consts.REFRESH_TOKEN_KEY_PREFIX + refreshToken;
            await _redisManager.ExecuteAsync(db => db.KeyDeleteAsync(key));
        }
    }
}
