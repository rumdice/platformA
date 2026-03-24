namespace PlatformA.MatchingEngine
{
    /// <summary>
    /// 주식 매도 매수 엔진
    /// </summary>
    public class OrderBook
    {
        // 매수 대기열 (가격 높은 순) -> 내림차순 정렬기 필요
        private readonly SortedDictionary<decimal, Queue<Order>> _bids
            = new SortedDictionary<decimal, Queue<Order>>(Comparer<decimal>.Create((x, y) => y.CompareTo(x)));

        // 매도 대기열 (가격 낮은 순) -> 기본 오름차순
        private readonly SortedDictionary<decimal, Queue<Order>> _asks
            = new SortedDictionary<decimal, Queue<Order>>();

        // 주문 처리 (진입점)
        public void ProcessOrder(Order newOrder)
        {
            if (newOrder.Type == OrderType.Buy)
            {
                MatchOrder(newOrder, _asks, _bids); // 매수 주문은 매도창(_asks)을 보며 매칭 시도
            }
            else
            {
                MatchOrder(newOrder, _bids, _asks); // 매도 주문은 매수창(_bids)을 보며 매칭 시도
            }
        }

        // 현재 호가창 상태를 이쁘게 포장해서 반환하는 메서드
        public OrderBookSnapshot GetSnapshot()
        {
            return new OrderBookSnapshot
            {
                // 매도(Asks): 싼 가격이 위로 (오름차순)
                Asks = _asks.Select(k => new PriceLevel
                {
                    Price = k.Key,
                    Quantity = k.Value.Sum(o => o.Quantity) // 해당 가격대의 주문 잔량 합산
                }).ToList(),

                // 매수(Bids): 비싼 가격이 위로 (내림차순 - SortedDictionary 특성상 Key순회시 반대일 수 있으니 주의)
                // SortedDictionary<decimal> with Comparer는 이미 Key가 정렬되어 있음.
                // 여기서는 리스트로 변환만 하면 됨.
                Bids = _bids.Select(k => new PriceLevel
                {
                    Price = k.Key,
                    Quantity = k.Value.Sum(o => o.Quantity)
                }).ToList()
            };
        }

        // 매칭 로직 (제네릭함수 아님, 로직 동일)
        private void MatchOrder(Order newOrder, SortedDictionary<decimal, Queue<Order>> oppositeBook, SortedDictionary<decimal, Queue<Order>> myBook)
        {
            // 상대방 호가창을 순회 (가장 유리한 가격부터)
            // ToList()로 복사해서 순회해야 컬렉션 수정 시 에러가 안 남 (최적화 포인트 1)
            foreach (var priceLevel in oppositeBook.Keys.ToList())
            {
                // [매칭 조건 검사]
                // 매수 주문이면: 상대(매도) 가격이 내 가격보다 싸거나 같아야 함 (priceLevel <= newOrder.Price)
                // 매도 주문이면: 상대(매수) 가격이 내 가격보다 비싸거나 같아야 함 (priceLevel >= newOrder.Price)
                // (SortedDictionary 정렬 덕분에 그냥 순서대로 보면 됨, 단 가격 조건 안맞으면 즉시 중단)

                bool isMatchable = (newOrder.Type == OrderType.Buy && priceLevel <= newOrder.Price) ||
                                   (newOrder.Type == OrderType.Sell && priceLevel >= newOrder.Price);

                if (!isMatchable) break; // 더 이상 유리한 가격이 없음 -> 매칭 종료

                // 해당 가격대의 주문 큐 가져오기
                var queue = oppositeBook[priceLevel];

                while (queue.Count > 0 && newOrder.Quantity > 0)
                {
                    var opponentOrder = queue.Peek(); // 상대방 주문 확인

                    // 체결 수량 계산 (둘 중 작은 값)
                    long tradeQuantity = Math.Min(newOrder.Quantity, opponentOrder.Quantity);

                    // ⚡ 체결 발생! (여기서 DB 저장이나 이벤트 발송)
                    Console.WriteLine($"[체결] {newOrder.Type} 주문(ID:{newOrder.Id})과 {opponentOrder.Type} 주문(ID:{opponentOrder.Id})이 {priceLevel}원에 {tradeQuantity}개 체결!");

                    // 수량 차감
                    newOrder.Quantity -= tradeQuantity;
                    opponentOrder.Quantity -= tradeQuantity;

                    // 상대방 주문이 다 체결되었으면 큐에서 제거
                    if (opponentOrder.Quantity == 0)
                    {
                        queue.Dequeue();
                    }
                }

                // 큐가 비었으면 해당 가격대 삭제 (메모리 정리)
                if (queue.Count == 0)
                {
                    oppositeBook.Remove(priceLevel);
                }

                // 내 주문을 다 처리했으면 종료
                if (newOrder.Quantity == 0) break;
            }

            // 매칭을 다 시도했는데도 잔량이 남았으면? -> 내 호가창(myBook)에 등록
            if (newOrder.Quantity > 0)
            {
                AddOrderToBook(newOrder, myBook);
            }
        }

        private void AddOrderToBook(Order order, SortedDictionary<decimal, Queue<Order>> book)
        {
            if (!book.ContainsKey(order.Price))
            {
                book[order.Price] = new Queue<Order>();
            }
            book[order.Price].Enqueue(order);
            Console.WriteLine($"[대기] {order.Type} 주문(ID:{order.Id})이 {order.Price}원에 {order.Quantity}개 미체결 잔량 등록됨.");
        }

        // 현재 호가창 상태 출력 (디버깅용)
        public void PrintBook()
        {
            Console.WriteLine("\n--- 호가창 현황 ---");
            Console.WriteLine("[매도(Sell) - 비싼게 위로]");
            foreach (var ask in _asks.Reverse()) // 보기 좋게 역순 출력
            {
                Console.WriteLine($"Price: {ask.Key} | Vol: {ask.Value.Sum(o => o.Quantity)}");
            }
            Console.WriteLine("-------------------");
            Console.WriteLine("[매수(Buy) - 비싼게 위로]");
            foreach (var bid in _bids)
            {
                Console.WriteLine($"Price: {bid.Key} | Vol: {bid.Value.Sum(o => o.Quantity)}");
            }
            Console.WriteLine("-------------------\n");
        }

        // 전송용 DTO 클래스들
        public class OrderBookSnapshot
        {
            public List<PriceLevel> Asks { get; set; } = new();
            public List<PriceLevel> Bids { get; set; } = new();
        }

        public class PriceLevel
        {
            public decimal Price { get; set; }
            public long Quantity { get; set; }
        }
    }
}