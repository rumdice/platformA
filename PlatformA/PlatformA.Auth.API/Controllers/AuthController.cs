using Microsoft.AspNetCore.Mvc;
using PlatformA.Auth.API.Filters;
using PlatformA.Auth.API.Models;
using PlatformA.Auth.API.Services;
using PlatformA.Library.Common;

namespace PlatformA.Auth.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly RefreshTokenService _refreshTokenService;
        private readonly PlayerService _playerService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            RefreshTokenService refreshTokenService,
            PlayerService playerService,
            ILogger<AuthController> logger)
        {
            _refreshTokenService = refreshTokenService;
            _playerService = playerService;
            _logger = logger;
        }

        /// <summary>
        /// POST /api/auth/login
        /// 신규 유저는 자동 등록, 기존 유저는 비밀번호 검증 후
        /// Access Token(15분) + Refresh Token(7일)을 함께 발급합니다.
        /// </summary>
        [RedisRateLimit("login")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            int? playerId = await _playerService.LoginAsync(request.Username, request.Password);
            if (playerId == null)
            {
                _logger.LogWarning("[Auth] 로그인 실패 - 잘못된 비밀번호. Username: {Username}", request.Username);
                return Unauthorized(new { Message = "비밀번호가 올바르지 않습니다." });
            }

            string accessToken = TokenManager.GenerateJwtToken(playerId.Value);
            string refreshToken = TokenManager.GenerateRefreshToken();

            await _refreshTokenService.SaveAsync(refreshToken, playerId.Value);

            _logger.LogInformation("[Auth] 로그인 성공. PlayerId: {PlayerId}, Username: {Username}", playerId.Value, request.Username);
            return Ok(new LoginResponse
            {
                Success = true,
                Token = accessToken,
                RefreshToken = refreshToken,
                PlayerId = playerId.Value,
                Message = "로그인 성공"
            });
        }

        /// <summary>
        /// POST /api/auth/refresh
        /// Refresh Token으로 새 Access Token을 발급합니다.
        /// Token Rotation: 기존 Refresh Token을 폐기하고 새 Refresh Token도 함께 발급합니다.
        /// </summary>
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
        {
            // 원자적 GET+DEL: 동시 요청에서 동일 토큰이 두 번 사용되는 경쟁 조건 방지
            int? userId = await _refreshTokenService.GetAndRevokeAsync(request.RefreshToken);
            if (userId == null)
            {
                _logger.LogWarning("[Auth] Refresh Token 검증 실패 — 유효하지 않거나 만료됨 (또는 이미 사용됨)");
                return Unauthorized(new { Message = "유효하지 않거나 만료된 Refresh Token입니다." });
            }

            string newAccessToken = TokenManager.GenerateJwtToken(userId.Value);
            string newRefreshToken = TokenManager.GenerateRefreshToken();
            await _refreshTokenService.SaveAsync(newRefreshToken, userId.Value);

            _logger.LogInformation("[Auth] Token 갱신 완료. UserId: {UserId}", userId.Value);
            return Ok(new
            {
                Token = newAccessToken,
                RefreshToken = newRefreshToken
            });
        }

        /// <summary>
        /// POST /api/auth/logout
        /// Refresh Token을 Redis에서 즉시 삭제하여 강제 무효화합니다.
        /// Access Token은 자연 만료(15분)를 기다립니다.
        /// </summary>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            // 원자적 GET+DEL: 검증과 폐기를 동시에 처리
            int? userId = await _refreshTokenService.GetAndRevokeAsync(request.RefreshToken);
            if (userId == null)
            {
                _logger.LogWarning("[Auth] 로그아웃 실패 — 유효하지 않거나 이미 만료된 Refresh Token");
                return Unauthorized(new { Message = "유효하지 않거나 이미 만료된 Refresh Token입니다." });
            }

            _logger.LogInformation("[Auth] 로그아웃 완료. Refresh Token 폐기. UserId: {UserId}", userId.Value);
            return Ok(new { Message = "로그아웃 완료." });
        }
    }
}
