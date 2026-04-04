using Microsoft.AspNetCore.Mvc;
using PlatformA.Library.Common;
using PlatformA.Ticketing.API.Filters;
using PlatformA.Ticketing.API.Services;

namespace PlatformA.Ticketing.API.Controllers
{
    [ApiController]
    [Route("api/queue")]
    public class QueueController : ControllerBase
    {
        private readonly QueueService _queueService;
        private readonly ILogger<QueueController> _logger;

        public QueueController(QueueService queueService, ILogger<QueueController> logger)
        {
            _queueService = queueService;
            _logger = logger;
        }

        // 1. 대기열 진입 (번호표 발급)
        [RedisRateLimit("queue")]
        [HttpPost("enter")]
        public async Task<IActionResult> EnterQueue()
        {
            try
            {
                int userId = GetUserIdFromToken();
                if (userId <= 0)
                {
                    _logger.LogWarning("[Queue] 유효하지 않은 토큰으로 대기열 진입 시도");
                    return Unauthorized(new { Message = "유효하지 않은 토큰입니다." });
                }

                await _queueService.UpdateHeartbeatAsync(userId);

                var res = await _queueService.RegisterQueueAsync(userId);
                if (res == false)
                    return BadRequest(new { Message = "대기열 큐가 오버했다." });

                return Ok($"대기열 등록 완료. UserId: {userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Queue] EnterQueue 처리 중 예외 발생");
                return BadRequest(ex.Message);
            }
        }

        // 2. 상태 확인 (폴링)
        [RedisRateLimit("queue")]
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            int userId = GetUserIdFromToken();
            if (userId <= 0)
                return Unauthorized(new { Message = "유효하지 않은 토큰입니다." });

            bool isActive = await _queueService.IsActiveAsync(userId);
            if (isActive)
                return Ok(new { UserId = userId, Rank = 0, Status = "Active", NextPollDelay = 0 });

            // 폴링 중인 유저의 하트비트 갱신 — Ghost 오탐 방지
            await _queueService.UpdateHeartbeatAsync(userId);

            long? rank = await _queueService.GetRankAsync(userId);
            if (rank.HasValue)
            {
                int delayMs = CalculateSmartPollDelay(rank.Value);
                return Ok(new { UserId = userId, Rank = rank.Value, Status = "Waiting", NextPollDelay = delayMs });
            }

            return NotFound(new { Message = "대기열 정보가 없습니다. 다시 진입해주세요." });
        }

        // 3. 대기열 명시적 이탈
        [RedisRateLimit("queue")]
        [HttpPost("leave")]
        public async Task<IActionResult> LeaveQueue()
        {
            try
            {
                int userId = GetUserIdFromToken();
                if (userId <= 0)
                    return Unauthorized(new { Message = "유효하지 않은 토큰입니다." });

                bool wasInQueue = await _queueService.LeaveQueueAsync(userId);
                if (wasInQueue)
                    return Ok(new { Message = $"대기열 이탈 완료. UserId: {userId}" });

                bool isActive = await _queueService.IsActiveAsync(userId);
                if (isActive)
                    return BadRequest(new { Message = "이미 입장권이 발급된 상태입니다. 게임 서버에 접속하거나 입장권 만료를 기다려주세요." });

                return NotFound(new { Message = "대기열에 없는 유저입니다." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Queue] LeaveQueue 처리 중 예외 발생");
                return BadRequest(ex.Message);
            }
        }

        private int GetUserIdFromToken()
        {
            string authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer"))
            {
                _logger.LogDebug("[Queue] Authorization 헤더 없음 또는 형식 오류: {Header}", authHeader);
                return -1;
            }

            string jwtToken = authHeader.Substring(7).Trim();
            return TokenManager.ValidateTokenAndGetUserId(jwtToken);
        }

        private static int CalculateSmartPollDelay(long rank)
        {
            if (rank > 100) return 10000;
            if (rank > 50) return 5000;
            if (rank > 10) return 3000;
            return Math.Max(10, (int)(1000 / Math.Sqrt(rank + 1)));
        }
    }
}
