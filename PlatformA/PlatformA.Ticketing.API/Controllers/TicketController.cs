using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace PlatformA.Ticketing.API.Controllers
{
    [ApiController]
    [Route("api/tickets")]
    public class TicketController : ControllerBase
    {
        private readonly IDatabase _redis;

        public TicketController(IConnectionMultiplexer redis)
        {
            _redis = redis.GetDatabase();
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
    }
}