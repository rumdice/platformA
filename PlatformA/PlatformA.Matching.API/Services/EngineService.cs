
using Microsoft.AspNetCore.SignalR;
using PlatformA.Matching.API.Hubs;
using PlatformA.MatchingEngine;
using System.Threading.Channels;

namespace PlatformA.Matching.API.Services
{
    // 기존에 사용하던 주식 매도/매수 매칭 엔진을 백그라운드 서비스로 구현.

    public class EngineService : BackgroundService
    {

        private readonly OrderBook _orderBook;
        private readonly Channel<Order> _orderChannel; // 고속 비동기 큐
        private readonly ILogger<EngineService> _logger;
        private readonly IHubContext<MatchingHub> _hubContext; // 방송 장비 추가

        public EngineService(ILogger<EngineService> logger, IHubContext<MatchingHub> hubContext)
        {
            _logger = logger;
            _orderBook = new OrderBook(); // 매칭 엔진 인스턴스 (이 안에는 lock이 없음!)

            // 큐 옵션: SingleReader(읽는 놈은 나 하나) => 최적화 옵션
            var options = new UnboundedChannelOptions { SingleReader = true, SingleWriter = false };
            _orderChannel = Channel.CreateUnbounded<Order>(options);

            _hubContext = hubContext; // 생성자 주입
        }

        // [Producer 역할] 컨트롤러가 호출함
        // 주문을 큐에 쏙 넣고 바로 리턴 (Fire-and-Forget)
        public async ValueTask EnqueueOrderAsync(Order order)
        {
            await _orderChannel.Writer.WriteAsync(order);
        }

        // [Consumer 역할] 백그라운드에서 무한루프
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 매칭 엔진 워커 가동! (Single Thread Logic)");

            // 큐에서 데이터가 들어올 때까지 대기하다가, 들어오면 하나씩 꺼냄
            await foreach (var order in _orderChannel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    // 🔥 핵심: 이 블록은 절대 동시에 실행되지 않음 (순차 처리 보장)
                    // 따라서 OrderBook 내부에서 lock을 걸 필요가 전혀 없음.
                    // 매도 매수 체결 매칭 로직을 수행한다.
                    _orderBook.ProcessOrder(order);

                    // 2. 🔥 [추가] 방송 송출!
                    // "ReceiveOrderBook"이라는 이름으로 스냅샷 데이터를 쏨.
                    var snapshot = _orderBook.GetSnapshot();

                    await _hubContext.Clients.All.SendAsync("ReceiveOrderBook", snapshot, cancellationToken: stoppingToken);

                    // (선택) 체결/대기 로그도 방송 가능
                    await _hubContext.Clients.All.SendAsync("ReceiveLog", $"주문(ID:{order.Id}) 처리됨", cancellationToken: stoppingToken);

                    // (콘솔 로그 대신 여기서 간단히 로그 찍어보기)
                    // _logger.LogInformation($"주문 처리 완료: ID {order.Id}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"주문 처리 중 에러 발생: {order.Id}");
                }
            }
        }

        // (디버깅용) 현재 호가창 객체 반환
        public OrderBook GetOrderBook() => _orderBook;

    }
}
