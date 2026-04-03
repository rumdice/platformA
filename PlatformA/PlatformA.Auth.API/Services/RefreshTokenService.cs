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

        public async Task RevokeAsync(string refreshToken)
        {
            string key = Consts.REFRESH_TOKEN_KEY_PREFIX + refreshToken;
            await _redisManager.ExecuteAsync(db => db.KeyDeleteAsync(key));
        }
    }
}
