using System.ComponentModel.DataAnnotations;

namespace PlatformA.Matching.API.Models
{
    public class MatchRequestDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "UserId는 1 이상이어야 합니다.")]
        public int UserId { get; set; }

        [Required]
        [MaxLength(50)]
        public string GameType { get; set; } = "gomoku";
    }

    public class MatchResultDto
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string RoomId { get; set; } = string.Empty;
    }

    public class MatchHistoryDto
    {
        public long MatchId { get; set; }
        public string GameType { get; set; } = string.Empty;
        public int OpponentId { get; set; }
        public string Result { get; set; } = string.Empty;
        public DateTime MatchedAt { get; set; }
    }

    /// <summary>Game.Gomoku → Matching.API 게임 시작 알림 DTO.</summary>
    public class MatchStartNotifyDto
    {
        [Required]
        [MaxLength(100)]
        public string RoomId { get; set; } = string.Empty;
    }

    /// <summary>Game.Gomoku → Matching.API 게임 결과 보고 DTO.</summary>
    public class MatchResultReportDto
    {
        [Required]
        [MaxLength(100)]
        public string RoomId { get; set; } = string.Empty;

        [Range(0, int.MaxValue, ErrorMessage = "WinnerId는 0(무승부) 이상이어야 합니다.")]
        public int WinnerId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Reason { get; set; } = string.Empty;
    }
}
