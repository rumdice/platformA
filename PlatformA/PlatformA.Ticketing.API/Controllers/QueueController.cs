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

        // 토큰 검증 함수
        private int GetUserIdFromToken()
        {
            string authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ")) return -1;

            string jwtToken = authHeader.Substring(7);
            return TokenManager.ValidateTokenAndGetUserId(jwtToken);
        }


        /// <summary>
        /// 스마트 풀링 계산.
        /// </summary>
        /// <param name="rank"></param>
        /// <returns></returns>
        private int CalculateSmartPollDelay(long rank)
        {
            // 등수에 따른 지연시간을 계산식으로 변환
            if (rank > 1000) return 10000; // 1,000등 밖: 10초
            if (rank > 500) return 5000;  // 500등 밖: 5초
            if (rank > 100) return 3000;  // 100등 밖: 3초

            // 100등 안: 1초
            return Math.Max(1000, (int)(10000 / Math.Sqrt(rank + 1)));
        }
    }
}