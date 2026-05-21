namespace PlatformA.Utils.API.Models.DB
{
    public class ShortUrl
    {
        // [Key]와 [DatabaseGenerated(DatabaseGeneratedOption.None)]를 써서
        // "DB야, ID는 내가 만들어서 넣어줄 테니 건드리지 마(Auto Increment 끔)"라고 알려줍니다.
        [System.ComponentModel.DataAnnotations.Key]
        [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.None)]
        public long Id { get; set; } // 고유 번호 (PK). Snowflake ID는 long(64bit 정수)

        public string? Code { get; set; } // 단축 코드 (6자리)
        public string? OriginalUrl { get; set; } // 원본 URL
        public DateTime CreatedAt { get; set; } = DateTime.Now; // 생성일

        public int ClickCount { get; set; } = 0; // 클릭 카운트.
    }
}
