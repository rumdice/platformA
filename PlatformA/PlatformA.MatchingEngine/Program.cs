using PlatformA.MatchingEngine;

Console.WriteLine("=== 고성능 매칭 엔진 시뮬레이터 ===");

var orderBook = new OrderBook();
long orderId = 1;

// 1. 매도(팔려는) 주문 미리 깔아두기
// 100원에 10개, 105원에 20개
orderBook.ProcessOrder(new Order(orderId++, OrderType.Sell, 100, 10));
orderBook.ProcessOrder(new Order(orderId++, OrderType.Sell, 105, 20));

orderBook.PrintBook();

// 2. 매수(사려는) 주문 들어옴
// 시나리오 A: 90원에 사겠다 (너무 싸서 체결 안됨 -> 대기열 등록)
Console.WriteLine(">> 주문 투입: 90원에 5개 매수");
orderBook.ProcessOrder(new Order(orderId++, OrderType.Buy, 90, 5));
orderBook.PrintBook();

// 시나리오 B: 102원에 15개 사겠다 (시장가보다 비싸게 부름)
// 예상: 100원짜리 10개 다 사먹고(체결), 105원은 못 사니까(비싸서) 남은 5개는 102원에 대기(Pending).
Console.WriteLine(">> 주문 투입: 102원에 15개 매수");
orderBook.ProcessOrder(new Order(orderId++, OrderType.Buy, 102, 15));

orderBook.PrintBook();