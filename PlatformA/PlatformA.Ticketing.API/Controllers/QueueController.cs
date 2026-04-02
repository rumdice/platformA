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

        public QueueController(QueueService queueService)
        {
            _queueService = queueService;
        }

        // 1. 대기열 진입 (번호표 발급)
        // POST /api/queue/enter?userId=user1
        [RedisRateLimit("queue")]
        [HttpPost("enter")]
        public async Task<IActionResult> EnterQueue()
        {
            try
            {
                int userId = GetUserIdFromToken();
                if (userId <= 0)
                {
                    Console.WriteLine($"UserID::::{userId}");
                    return Unauthorized(new { Message = "유효하지 않은 토큰입니다." });
                }

                // 클라이언트가 상태를 물어볼 때마다 생존(Heartbeat) 갱신!
                await _queueService.UpdateHeartbeatAsync(userId);

                // Redis ZSET에 유저 밀어넣기 (내부적으로 원자적 검사+삽입)
                var res = await _queueService.RegisterQueueAsync(userId);
                if (res == false)
                {
                    return BadRequest(new { Message = "대기열 큐가 오버했다." });
                }

                return Ok($"대기열 등록 완료. UserId: {userId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return BadRequest(ex.Message);
            }
        }

        // 2. 상태 확인 (폴링 - 클라이언트가 주기적으로 호출)
        // GET /api/queue/status?userId=user1
        [RedisRateLimit("queue")]
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
                return Ok(new { UserId = userId, Rank = 0, Status = "Active", NextPollDelay = 0 });
            }

            // 계속 대기열이라면 대기열 등수를 판단.
            long? rank = await _queueService.GetRankAsync(userId);
            if (rank.HasValue)
            {
                // 스마트 풀링 - 기다릴 순번을 계산해서 돌려준다. 클라는 해당 시간 후에 Status를 던진다.
                int delayMs = CalculateSmartPollDelay(rank.Value);

                return Ok(new { UserId = userId, Rank = rank.Value, Status = "Waiting", NextPollDelay = delayMs });
            }

            // 큐에도 없고, Active에도 없으면? (비정상 이탈)
            return NotFound(new { Message = "대기열 정보가 없습니다. 다시 진입해주세요." });
        }

        // 3. 대기열 명시적 이탈
        // POST /api/queue/leave
        [RedisRateLimit("queue")]
        [HttpPost("leave")]
        public async Task<IActionResult> LeaveQueue()
        {
            try
            {
                int userId = GetUserIdFromToken();
                if (userId <= 0)
                    return Unauthorized(new { Message = "유효하지 않은 토큰입니다." });

                // 대기열에서 제거 시도 (QUEUE_KEY + heartbeats 원자적 제거)
                bool wasInQueue = await _queueService.LeaveQueueAsync(userId);
                if (wasInQueue)
                    return Ok(new { Message = $"대기열 이탈 완료. UserId: {userId}" });

                // 큐에 없는 경우 — Active 상태인지 추가 확인
                bool isActive = await _queueService.IsActiveAsync(userId);
                if (isActive)
                    return BadRequest(new { Message = "이미 입장권이 발급된 상태입니다. 게임 서버에 접속하거나 입장권 만료를 기다려주세요." });

                // 큐에도 없고 Active도 아님
                return NotFound(new { Message = "대기열에 없는 유저입니다." });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return BadRequest(ex.Message);
            }
        }

        // 토큰 검증 함수
        private int GetUserIdFromToken()
        {
            string authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer"))
            {
                Console.WriteLine($"authHeader-----{authHeader}");
                return -1;
            }

            string jwtToken = authHeader.Substring(7).Trim();
            return TokenManager.ValidateTokenAndGetUserId(jwtToken);
        }


        /// <summary>
        /// 스마트 풀링 계산.
        /// </summary>
        private int CalculateSmartPollDelay(long rank)
        {
            // 등수에 따른 지연시간을 계산식으로 변환
            if (rank > 100) return 10000; // 1,00등 밖: 10초
            if (rank > 50) return 5000;  // 50등 밖: 5초
            if (rank > 10) return 3000;  // 10등 밖: 3초

            // 10등 안 1초
            return Math.Max(10, (int)(1000 / Math.Sqrt(rank + 1)));
        }
    }
}
