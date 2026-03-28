using Microsoft.AspNetCore.Mvc;
using PlatformA.Library.Common;
using PlatformA.Matching.API.Services;

namespace PlatformA.Matching.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameMatchController : ControllerBase
    {
        private readonly GameMatchService _matchService;

        public GameMatchController(GameMatchService matchService)
        {
            _matchService = matchService;
        }

        /// <summary>
        /// 매칭 요청 API: 클라이언트가 매칭을 요청할 때 호출하는 엔드포인트입니다.
        /// </summary>
        [HttpPost("RequestMatch")]
        public async Task<IActionResult> RequestMatch()
        {
            // 1. HTTP 헤더에서 Authorization: Bearer <token> 추출
            string authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized(new { Message = "토큰이 없습니다." });
            }

            string jwtToken = authHeader.Substring(7);

            // 2. 토큰 검증 및 유저 ID 파싱 (허브에서 하던 것과 동일!)
            int playerId = TokenManager.ValidateTokenAndGetUserId(jwtToken);
            if (playerId <= 0)
            {
                return Unauthorized(new { Message = "유효하지 않은 토큰입니다." });
            }

            // 3. 백그라운드 엔진의 Redis 대기열로 밀어넣기
            await _matchService.AddPlayerToQueueAsync(playerId);

            // 4. 요청을 처리했으니 즉시 HTTP 연결(스레드)을 끊고 200 OK 반환!
            return Ok(new { Message = "매칭 대기열에 성공적으로 진입했습니다." });
        }

        /// <summary>
        /// 테스트 API (안쓰임): 실제로 매칭 로직은 API Request 가 아니라 SignalR Hub에서 하는 방식으로 개선.
        /// </summary>
        [HttpPost("test-match")]
        public async Task<IActionResult> TestMatch([FromBody] MatchTestRequest request)
        {
            Console.WriteLine($"[TestAPI] 매칭 테스트 요청 수신: User({request.User1Id}) & User({request.User2Id})");

            return Ok(new { Message = "Matching event published to Redis!", RoomId = 101 }); // 임시 방번호 응답
        }
    }

    public class MatchTestRequest
    {
        public int User1Id { get; set; }
        public int User2Id { get; set; }
    }
}