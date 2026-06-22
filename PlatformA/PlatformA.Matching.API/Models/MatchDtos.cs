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
        public string Host   { get; set; } = string.Empty;
        public int    Port   { get; set; }
        public string RoomId { get; set; } = string.Empty;
    }

    public class MatchHistoryDto
    {
        public long     MatchId    { get; set; }
        public string   GameType   { get; set; } = string.Empty;
        public int      OpponentId { get; set; }
        public string   Result     { get; set; } = string.Empty;
        public DateTime MatchedAt  { get; set; }
    }
}
