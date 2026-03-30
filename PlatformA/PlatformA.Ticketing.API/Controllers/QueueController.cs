using Microsoft.AspNetCore.Mvc;
using PlatformA.Library.Common;
using PlatformA.Ticketing.API.Services;

namespace PlatformA.Ticketing.API.Controllers
{
    [ApiController]
    [Route("api/queue")]
    public class QueueController : ControllerBase
    {
        private readonly QueueService _queueService;

        public QueueController(QueueService queueService)
        {
            _queueService = queueService;
        }

        // 1. 대기열 진입 (번호표 발급)
        // POST /api/queue/enter?userId=user1
        [HttpPost("enter")]
        public async Task<IActionResult> EnterQueue()
        {
            int userId = GetUserIdFromToken();
            if (userId <= 0) 
                return Unauthorized(new { Message = "유효하지 않은 토큰입니다." });

            // Redis ZSET에 유저 밀어넣기
            await _queueService.RegisterQueueAsync(userId);

            return Ok($"대기열 등록 완료. UserId: {userId}");
        }

        // 2. 상태 확인 (폴링 - 클라이언트가 주기적으로 호출)
        // GET /api/queue/status?userId=user1
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            int userId = GetUserIdFromToken();
            if (userId <= 0) 
                return Unauthorized(new { Message = "유효하지 않은 토큰입니다." });

            // 대기열에서 입장가능한 상태인지 판단.
            bool isActive = await _queueService.IsActiveAsync(userId);
            if (isActive)
            {
                return Ok(new { UserId = userId, Rank = 0, Status = "Active" });
            }

            // 계속 대기열이라면 대기열 등수를 판단.
            long? rank = await _queueService.GetRankAsync(userId);
            if (rank.HasValue)
            {
                return Ok(new { UserId = userId, Rank = rank.Value, Status = "Waiting" });
            }

            // 큐에도 없고, Active에도 없으면? (비정상 이탈)
            return NotFound(new { Message = "대기열 정보가 없습니다. 다시 진입해주세요." });
        }

        // 토큰 검증 함수
        private int GetUserIdFromToken()
        {
            string authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ")) return -1;

            string jwtToken = authHeader.Substring(7);
            return TokenManager.ValidateTokenAndGetUserId(jwtToken);
        }
    }
}