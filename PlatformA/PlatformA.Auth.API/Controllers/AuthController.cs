using Microsoft.AspNetCore.Mvc;
using PlatformA.Auth.API.Filters;
using PlatformA.Auth.API.Models;
using PlatformA.Library.Common;

namespace PlatformA.Auth.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private static int playerId = 0; // TODO: static 이라 서버 재시작 하면 0부터 시작. playerId 겹침. DB 도입 작업 후 제거.

        /// <summary>
        /// POST 요청: /api/auth/login (더미 계정 검증 + playerId 자동 증가)
        /// </summary>

        [RedisRateLimit("login")]
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            // TODO: DB 검증 등 절차가 필요
            var playerId = Interlocked.Increment(ref AuthController.playerId); // playerId 자동 증가 (발급)

            // JWT 토큰 생성 (playerId 포함)
            string token = TokenManager.GenerateJwtToken(playerId);

            return Ok(new LoginResponse
            {
                Success = true,
                Token = token,
                PlayerId = playerId,
                Message = "로그인 성공"
            });

        }
    }
}
