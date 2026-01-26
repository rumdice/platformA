namespace PlatformA.Utils.API.Models.DB
{
    // 데이터 모델
    public class ShortUrl
    {
        public int Id { get; set; } // 고유 번호 (PK)
        public string Code { get; set; } // 단축 코드 (6자리)
        public string OriginalUrl { get; set; } // 원본 URL
        public DateTime CreatedAt { get; set; } = DateTime.Now; // 생성일

        public int ClickCount { get; set; } = 0; // 클릭 카운트.
    }
}
