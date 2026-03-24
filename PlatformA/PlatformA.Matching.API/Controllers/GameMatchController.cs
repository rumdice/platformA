using Microsoft.AspNetCore.Mvc;
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