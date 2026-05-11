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
        /// 매칭 요청: Bearer JWT로 인증 후 매칭 대기열에 진입합니다.
        /// </summary>
        [HttpPost("RequestMatch")]
        public async Task<IActionResult> RequestMatch()
        {
            int playerId = ExtractPlayerId();
            if (playerId <= 0)
                return Unauthorized(new { Message = "유효하지 않은 토큰입니다." });

            await _matchService.AddPlayerToQueueAsync(playerId);
            return Ok(new { Message = "매칭 대기열에 성공적으로 진입했습니다." });
        }

        /// <summary>
        /// 매칭 취소: 대기열에서 본인을 제거합니다.
        /// </summary>
        [HttpDelete("CancelMatch")]
        public async Task<IActionResult> CancelMatch()
        {
            int playerId = ExtractPlayerId();
            if (playerId <= 0)
                return Unauthorized(new { Message = "유효하지 않은 토큰입니다." });

            bool removed = await _matchService.RemovePlayerFromQueueAsync(playerId);
            return removed
                ? Ok(new { Message = "매칭이 취소되었습니다." })
                : NotFound(new { Message = "대기열에서 찾을 수 없습니다." });
        }

        /// <summary>
        /// 대기열 상태 조회: 본인의 순위와 전체 대기 인원을 반환합니다.
        /// </summary>
        [HttpGet("Status")]
        public async Task<IActionResult> GetStatus()
        {
            int playerId = ExtractPlayerId();
            if (playerId <= 0)
                return Unauthorized(new { Message = "유효하지 않은 토큰입니다." });

            long rank = await _matchService.GetQueueRankAsync(playerId);
            if (rank < 0)
                return NotFound(new { Message = "매칭 대기열에 없습니다." });

            long total = await _matchService.GetQueueLengthAsync();
            return Ok(new { Rank = rank + 1, Total = total });
        }

        /// <summary>
        /// 테스트 API (미사용): 실제 매칭 로직은 RequestMatch + 백그라운드 워커가 처리.
        /// </summary>
        [HttpPost("test-match")]
        public IActionResult TestMatch([FromBody] MatchTestRequest request)
        {
            Console.WriteLine($"[TestAPI] 매칭 테스트 요청 수신: User({request.User1Id}) & User({request.User2Id})");
            return Ok(new { Message = "Matching event published to Redis!", RoomId = 101 });
        }

        private int ExtractPlayerId()
        {
            string authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer"))
                return 0;
            string jwtToken = authHeader.Substring(7);
            return TokenManager.ValidateTokenAndGetUserId(jwtToken);
        }
    }

    public class MatchTestRequest
    {
        public int User1Id { get; set; }
        public int User2Id { get; set; }
    }
}
