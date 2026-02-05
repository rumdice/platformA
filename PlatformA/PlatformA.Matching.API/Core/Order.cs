using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace PlatformA.MatchingEngine
{
    public enum OrderType { Buy, Sell }

    public class Order
    {
        public long Id { get; set; }           // 주문 ID
        public OrderType Type { get; set; }    // 매수/매도
        public decimal Price { get; set; }     // 가격
        public long Quantity { get; set; }     // 수량 (잔량)
        public long OriginalQuantity { get; set; } // 최초 주문 수량 (통계용)
        public DateTime Timestamp { get; set; } // 시간 우선 원칙용

        public Order(long id, OrderType type, decimal price, long quantity)
        {
            Id = id;
            Type = type;
            Price = price;
            Quantity = quantity;
            OriginalQuantity = quantity;
            Timestamp = DateTime.UtcNow;
        }
    }
}