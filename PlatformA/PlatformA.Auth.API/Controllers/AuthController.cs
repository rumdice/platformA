using Microsoft.AspNetCore.Mvc;
using PlatformA.Auth.API.Models;
using PlatformA.Library.Common;

namespace PlatformA.Auth.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private static int playerId = 0;

        /// <summary>
        /// POST 요청: /api/auth/login (더미 계정 검증 + playerId 자동 증가)
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            // TODO: DB 검증 등 절차가 필요

            var playerId = Interlocked.Increment(ref AuthController.playerId); // playerId 자동 증가

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