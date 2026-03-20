using Microsoft.AspNetCore.Mvc;
using PlatformA.Matching.API.Services;

namespace PlatformA.Matching.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameMatchController : ControllerBase
    {
        private readonly GameMatchService _matchService;

        // 생성자에서 싱글톤으로 등록된 EngineService를 주입받습니다.
        public GameMatchController(GameMatchService matchService)
        {
            _matchService = matchService;
        }

        [HttpPost("test-match")]
        public async Task<IActionResult> TestMatch([FromBody] MatchTestRequest request)
        {
            // 실제 서비스에서는 큐에서 꺼내겠지만, 테스트를 위해 두 유저 ID를 강제로 매칭시킵니다.
            Console.WriteLine($"[TestAPI] 매칭 테스트 요청 수신: User({request.User1Id}) & User({request.User2Id})");

            await _matchService.ProcessMatchingAsync(request.User1Id, request.User2Id);

            return Ok(new { Message = "Matching event published to Redis!", RoomId = 101 }); // 임시 방번호 응답
        }
    }

    public class MatchTestRequest
    {
        public int User1Id { get; set; }
        public int User2Id { get; set; }
    }
}