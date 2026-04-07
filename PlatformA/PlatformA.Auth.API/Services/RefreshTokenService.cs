using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using PlatformA.Library.Common;
using PlatformA.Library.Core;

namespace PlatformA.Auth.API.Services
{
    /// <summary>
    /// Redis 기반 Refresh Token 관리 서비스.
    ///
    /// [키 구조] refresh:{playerId} → refreshToken 문자열
    /// - 유저 1명당 Redis 키가 정확히 1개이므로, 재로그인 시 SET 덮어쓰기만으로
    ///   기존 토큰이 자동 폐기됩니다 (단일 세션 정책, 중복 로그인 불허).
    /// - Refresh Token 포맷: "{playerId}:{random}" — playerId를 파싱하여
    ///   역방향 인덱스 없이 Redis 키를 직접 계산합니다.
    ///
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

        /// <summary>
        /// Refresh Token을 Redis에 저장합니다.
        /// 키: refresh:{userId} — 기존 토큰이 있으면 덮어써서 자동 폐기합니다 (재로그인 시 단일 세션 보장).
        /// </summary>
        public async Task SaveAsync(string refreshToken, int userId)
        {
            string key = Consts.REFRESH_TOKEN_KEY_PREFIX + userId;
            await _redisManager.ExecuteAsync(db =>
                db.StringSetAsync(key, refreshToken,
                    TimeSpan.FromDays(Consts.REFRESH_TOKEN_EXPIRY_DAYS)));
        }

        /// <summary>
        /// 토큰 검증과 폐기를 원자적으로 수행합니다 (Lua GET+COMPARE+DEL).
        /// 1. 토큰에서 playerId를 파싱하여 Redis 키를 계산합니다.
        /// 2. 저장된 토큰과 요청 토큰을 비교합니다 (토큰 재사용 공격 방지).
        /// 3. 일치하면 원자적으로 삭제하고 playerId를 반환합니다.
        /// </summary>
        public async Task<int?> GetAndRevokeAsync(string refreshToken)
        {
            int? playerId = TokenManager.ParsePlayerIdFromRefreshToken(refreshToken);
            if (playerId == null)
            {
                _logger.LogWarning("[RefreshToken] 토큰 포맷 오류 — playerId 파싱 실패");
                return null;
            }

            string key = Consts.REFRESH_TOKEN_KEY_PREFIX + playerId;

            // GET + COMPARE + DEL 을 하나의 Lua 트랜잭션으로 처리.
            // 저장된 토큰과 요청 토큰이 일치할 때만 삭제하여
            // 재로그인으로 교체된 구형 토큰의 재사용을 차단합니다.
            const string script = @"
                local stored = redis.call('get', KEYS[1])
                if stored and stored == ARGV[1] then
                    redis.call('del', KEYS[1])
                    return 1
                end
                return nil";
            try
            {
                var result = await _redisManager.ExecuteAsync(db =>
                    db.ScriptEvaluateAsync(script,
                        new StackExchange.Redis.RedisKey[] { key },
                        new StackExchange.Redis.RedisValue[] { refreshToken }));

                if (result.IsNull) return null;
                return playerId;
            }
            catch (BrokenCircuitException)
            {
                _logger.LogWarning("[RefreshToken] 회로차단기 개방 — 원자적 토큰 검증 불가");
                return null;
            }
        }

        /// <summary>
        /// Refresh Token을 즉시 폐기합니다 (로그아웃 외 강제 무효화 용도).
        /// </summary>
        public async Task RevokeAsync(string refreshToken)
        {
            int? playerId = TokenManager.ParsePlayerIdFromRefreshToken(refreshToken);
            if (playerId == null) return;
            string key = Consts.REFRESH_TOKEN_KEY_PREFIX + playerId;
            await _redisManager.ExecuteAsync(db => db.KeyDeleteAsync(key));
        }
    }
}
