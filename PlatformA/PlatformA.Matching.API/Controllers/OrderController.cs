using Microsoft.AspNetCore.Mvc;
using PlatformA.Matching.API.Services;
using PlatformA.MatchingEngine;


namespace PlatformA.Matching.API.Controllers
{
    /// <summary>
    /// 주식 매도/매수 벡엔드 API
    /// </summary>
    [ApiController]
    [Route("api/orders")]
    public class OrderController : ControllerBase
    {
        private readonly EngineService _engine;

        // 간단한 ID 카운터 (나중에 Snowflake로 교체 가능)
        private static long _idCounter = 0;

        public OrderController(EngineService engine)
        {
            _engine = engine;
        }

        [HttpPost]
        public async Task<IActionResult> SubmitOrder([FromBody] OrderRequest request)
        {
            // ID 채번 (Thread-safe 증가)
            long newId = Interlocked.Increment(ref _idCounter);

            var order = new Order(
                newId,
                request.Type,
                request.Price,
                request.Quantity
            );

            await _engine.EnqueueOrderAsync(order);

            return Accepted(new { OrderId = newId, Message = "주문이 접수되었습니다." });
        }

        /// <summary>
        /// 디버깅용: 현재 호가창 상태 보기 (간이) 
        /// </summary>
        /// <returns></returns>
        [HttpGet("book")]
        public IActionResult GetOrderBookInfo()
        {
            // 원래는 DTO로 변환해서 줘야 하지만, 테스트용이라 그냥 호출
            // (OrderBook.PrintBook()이 콘솔에 찍히는 걸 확인하세요)
            _engine.GetOrderBook().PrintBook();
            return Ok("서버 콘솔(터미널)을 확인하세요.");
        }
    }

    // 요청 DTO
    public class OrderRequest
    {
        public OrderType Type { get; set; } // 0: Buy, 1: Sell
        public decimal Price { get; set; }
        public long Quantity { get; set; }
    }
}
