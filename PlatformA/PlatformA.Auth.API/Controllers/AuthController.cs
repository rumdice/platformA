using Microsoft.AspNetCore.Mvc;
using PlatformA.Auth.API.Models;
using PlatformA.Library.Common;

namespace PlatformA.Auth.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // 라우팅 주소: /api/auth
    public class AuthController : ControllerBase
    {
        private static int playerId = 0;

        // Test Json
        //
        //{
        //    "username": "test",
        //    "password": "1234"
        //}

        //[HttpPost("login")] // POST 요청: /api/auth/login
        //public IActionResult Login([FromBody] LoginRequest request)
        //{
        //    // 1. 간단한 더미 계정 검증 (DB 연동 전)
        //    if (request.Username == "test" && request.Password == "1234")
        //    {
        //        // DB에서 회원 정보를 찾았다고 가정합니다.
        //        int playerId = 777;

        //        // 2. JWT 토큰 생성
        //        string token = TokenManager.GenerateJwtToken(playerId);

        //        // 3. 클라이언트에게 성공(200 OK)과 함께 토큰 반환
        //        return Ok(new LoginResponse
        //        {
        //            Success = true,
        //            Token = token,
        //            PlayerId = playerId,
        //            Message = "로그인 성공"
        //        });
        //    }

        //    // 아이디나 비밀번호가 틀렸을 때 (401 Unauthorized) 반환
        //    return Unauthorized(new LoginResponse
        //    {
        //        Success = false,
        //        Message = "아이디 또는 비밀번호가 틀렸습니다."
        //    });
        //}


        [HttpPost("login")] // POST 요청: /api/auth/login (더미 계정 검증 + playerId 자동 증가)
        public IActionResult Login([FromBody] LoginRequest request)
        {
            //if (request.Username == "test" && request.Password == "1234")
            {
                // 2. JWT 토큰 생성
                var playerId = Interlocked.Increment(ref AuthController.playerId); // playerId 자동 증가

                string token = TokenManager.GenerateJwtToken(playerId);

                // 3. 클라이언트에게 성공(200 OK)과 함께 토큰 반환
                return Ok(new LoginResponse
                {
                    Success = true,
                    Token = token,
                    PlayerId = playerId,
                    Message = "로그인 성공"
                });
            }

            // 아이디나 비밀번호가 틀렸을 때 (401 Unauthorized) 반환
            return Unauthorized(new LoginResponse
            {
                Success = false,
                Message = "아이디 또는 비밀번호가 틀렸습니다."
            });
        }

    }
}