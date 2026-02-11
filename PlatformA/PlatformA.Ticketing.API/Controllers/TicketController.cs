using Microsoft.AspNetCore.Mvc;
using RedLockNet.SERedis;
using StackExchange.Redis;

namespace PlatformA.Ticketing.API.Controllers
{
    [ApiController]
    [Route("api/tickets")]
    public class TicketController : ControllerBase
    {
        private readonly IDatabase _redis;
        private readonly RedLockFactory _lockFactory; // 락 공장

        public TicketController(IConnectionMultiplexer redis, RedLockFactory lockFactory)
        {
            _redis = redis.GetDatabase();
            _lockFactory = lockFactory;
        }

        // 0. (준비) 티켓 수량 초기화 API
        // POST /api/tickets/reset?count=100
        [HttpPost("reset")]
        public async Task<IActionResult> Reset(int count = 100)
        {
            await _redis.StringSetAsync("ticket:iu_concert", count);
            return Ok($"아이유 콘서트 티켓 {count}장 발행 완료!");
        }

        // 1. (취약함) 티켓 예매 API
        // POST /api/tickets/buy-bad
        [HttpPost("buy-bad")]
        public async Task<IActionResult> BuyTicketBad()
        {
            var key = "ticket:iu_concert";

            // ---------------------------------------------------------
            // 🚨 [Race Condition 발생 구간]
            // Redis의 Atomic 명령어(Decrement)를 쓰지 않고, 
            // 일부러 값을 가져와서 C# 메모리에서 계산 후 덮어씁니다.
            // ---------------------------------------------------------

            // 1. 재고 확인 (Read)
            var currentStockValue = await _redis.StringGetAsync(key);
            int currentStock = (int)currentStockValue;

            if (currentStock <= 0)
            {
                return BadRequest("매진되었습니다.");
            }

            // TODO: (일부러 틈을 벌리기 위해 아주 짧은 지연 시간 추가 - 실제 DB I/O 시뮬레이션)
            await Task.Delay(10);

            // 2. 재고 차감 (Modify)
            int newStock = currentStock - 1;

            // 3. 저장 (Write)
            await _redis.StringSetAsync(key, newStock);

            return Ok($"예매 성공! 남은 표: {newStock}");
        }


        // 2. (안전함) 분산 락 적용 예매 API
        // POST /api/tickets/buy-good
        [HttpPost("buy-good")]
        public async Task<IActionResult> BuyTicketGood()
        {
            var key = "ticket:iu_concert";
            var lockKey = "lock:ticket:iu_concert"; // 락을 위한 별도 키

            // 🔥 1. 락 획득 시도
            // resource: 락 이름
            // expiryTime: 락 자동 만료 시간 (3초 지나면 강제 반납 - 데드락 방지)
            // waitTime: 락을 얻기 위해 기다리는 시간 (2초 동안 계속 노크함)
            // retryTime: 노크하는 간격 (0.1초마다 노크)
            using (var redLock = await _lockFactory.CreateLockAsync(lockKey, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(100)))
            {
                if (redLock.IsAcquired)
                {
                    // 🔒 [임계 구역 (Critical Section)] - 한 번에 한 명만 들어옴

                    // A. 재고 확인
                    var currentStockValue = await _redis.StringGetAsync(key);
                    int currentStock = (int)currentStockValue;

                    if (currentStock <= 0)
                    {
                        return BadRequest("매진되었습니다. (Safe)");
                    }

                    // (아까와 똑같은 딜레이를 줘도 안전한지 테스트)
                    await Task.Delay(10);

                    // B. 재고 차감
                    int newStock = currentStock - 1;

                    // C. 저장
                    await _redis.StringSetAsync(key, newStock);

                    return Ok($"예매 성공! 남은 표: {newStock}");

                    // (using 블록이 끝나면 자동으로 락 반납 - Unlock)
                }
                else
                {
                    // 락 획득 실패 (대기 시간 초과)
                    return StatusCode(429, "접속자가 너무 많아 실패했습니다. 다시 시도해주세요.");
                }
            }
        }
    }
}
