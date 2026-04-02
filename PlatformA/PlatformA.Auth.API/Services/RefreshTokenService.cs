using PlatformA.Library.Common;
using PlatformA.Library.Core;
using StackExchange.Redis;

namespace PlatformA.Auth.API.Services
{
    /// <summary>
    /// Redis 기반 Refresh Token 관리 서비스.
    ///
    /// [구조]
    /// Key:   refresh:{token}  (REFRESH_TOKEN_KEY_PREFIX + token)
    /// Value: {userId}
    /// TTL:   REFRESH_TOKEN_EXPIRY_DAYS (7일)
    ///
    /// JWT(Access Token)와 달리 서버 측에서 관리하므로
    /// 로그아웃, 강제 만료, 탈취 대응 시 즉시 무효화가 가능합니다.
    /// </summary>
    public class RefreshTokenService
    {
        private readonly IDatabase _db;

        public RefreshTokenService(RedisManager redisManager)
        {
            _db = redisManager.Connection.GetDatabase();
        }

        /// <summary>
        /// Refresh Token을 Redis에 저장합니다.
        /// </summary>
        public async Task SaveAsync(string refreshToken, int userId)
        {
            string key = Consts.REFRESH_TOKEN_KEY_PREFIX + refreshToken;
            await _db.StringSetAsync(
                key,
                userId.ToString(),
                TimeSpan.FromDays(Consts.REFRESH_TOKEN_EXPIRY_DAYS)
            );
        }

        /// <summary>
        /// Refresh Token으로 userId를 조회합니다. 유효하지 않으면 null 반환.
        /// </summary>
        public async Task<int?> GetUserIdAsync(string refreshToken)
        {
            string key = Consts.REFRESH_TOKEN_KEY_PREFIX + refreshToken;
            RedisValue value = await _db.StringGetAsync(key);

            if (value.IsNullOrEmpty)
                return null;

            return int.Parse(value!);
        }

        /// <summary>
        /// Refresh Token을 즉시 무효화합니다. (로그아웃, Token Rotation 시 사용)
        /// </summary>
        public async Task RevokeAsync(string refreshToken)
        {
            string key = Consts.REFRESH_TOKEN_KEY_PREFIX + refreshToken;
            await _db.KeyDeleteAsync(key);
        }
    }
}
