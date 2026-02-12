using Microsoft.AspNetCore.Mvc;
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
        public async Task<IActionResult> EnterQueue(string userId)
        {
            await _queueService.RegisterQueue(userId);
            return Ok($"대기열 등록 완료. UserId: {userId}");
        }

        // 2. 상태 확인 (폴링 - 클라이언트가 주기적으로 호출)
        // GET /api/queue/status?userId=user1
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus(string userId)
        {
            var (isAllowed, rank) = await _queueService.GetStatus(userId);

            if (rank == -1)
                return BadRequest("대기열에 등록되지 않은 유저입니다.");

            if (isAllowed)
            {
                return Ok(new { status = "Pass", message = "입장하세요! 구매 API를 호출할 수 있습니다." });
            }
            else
            {
                return Ok(new { status = "Wait", rank = rank, message = $"대기 중입니다. 현재 대기 순번: {rank}등" });
            }
        }
    }
}