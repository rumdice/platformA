namespace PlatformA.MySqlDB.Lib.DBWebApp.Entities
{
    /// <summary>
    /// 테이블: players
    /// 플랫폼 계정 정보. Auth.API 로그인/회원가입에서 사용합니다.
    /// </summary>
    public class Player
    {
        public int Id { get; set; }                          // id INT AUTO_INCREMENT PK
        public string Username { get; set; } = string.Empty; // username VARCHAR(20) UNIQUE NOT NULL
        public string PasswordHash { get; set; } = string.Empty; // password_hash VARCHAR(255) NOT NULL
        public DateTime CreatedAt { get; set; }              // created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP

        // 1:1 → player_stats
        public PlayerStat? Stat { get; set; }

        // 1:N → match_records (player1 / player2)
        public ICollection<MatchRecord> MatchesAsPlayer1 { get; set; } = [];
        public ICollection<MatchRecord> MatchesAsPlayer2 { get; set; } = [];
    }
}
