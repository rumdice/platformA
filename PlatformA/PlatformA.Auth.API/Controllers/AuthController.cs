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

        public AuthController(RefreshTokenService refreshTokenService, PlayerService playerService)
        {
            _refreshTokenService = refreshTokenService;
            _playerService = playerService;
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
                return Unauthorized(new { Message = "비밀번호가 올바르지 않습니다." });

            string accessToken = TokenManager.GenerateJwtToken(playerId.Value);
            string refreshToken = TokenManager.GenerateRefreshToken();

            // Refresh Token을 Redis에 저장 (TTL: 7일)
            await _refreshTokenService.SaveAsync(refreshToken, playerId.Value);

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
            // 1. Redis에서 Refresh Token 검증
            int? userId = await _refreshTokenService.GetUserIdAsync(request.RefreshToken);
            if (userId == null)
                return Unauthorized(new { Message = "유효하지 않거나 만료된 Refresh Token입니다." });

            // 2. Token Rotation: 기존 Refresh Token 폐기
            //    탈취된 토큰이 재사용되는 것을 방지합니다.
            await _refreshTokenService.RevokeAsync(request.RefreshToken);

            // 3. 새 Access Token + 새 Refresh Token 발급
            string newAccessToken = TokenManager.GenerateJwtToken(userId.Value);
            string newRefreshToken = TokenManager.GenerateRefreshToken();

            await _refreshTokenService.SaveAsync(newRefreshToken, userId.Value);

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
            // Refresh Token이 유효한지 먼저 확인
            int? userId = await _refreshTokenService.GetUserIdAsync(request.RefreshToken);
            if (userId == null)
                return Unauthorized(new { Message = "유효하지 않거나 이미 만료된 Refresh Token입니다." });

            await _refreshTokenService.RevokeAsync(request.RefreshToken);

            Console.WriteLine($"[Auth] 유저 {userId} 로그아웃 완료. Refresh Token 폐기.");
            return Ok(new { Message = "로그아웃 완료." });
        }
    }
}
