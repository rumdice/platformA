namespace PlatformA.MySqlDB.Lib.DBWebApp.Entities
{
    /// <summary>
    /// 테이블: player_stats
    /// 플레이어 누적 전적. players 와 1:1 관계입니다.
    /// </summary>
    public class PlayerStat
    {
        public int Id { get; set; }          // id INT AUTO_INCREMENT PK
        public int PlayerId { get; set; }    // player_id INT UNIQUE NOT NULL (FK → players.id)
        public int TotalGames { get; set; }  // total_games INT DEFAULT 0
        public int Wins { get; set; }        // wins INT DEFAULT 0
        public int Losses { get; set; }      // losses INT DEFAULT 0
        public DateTime UpdatedAt { get; set; } // updated_at DATETIME(6) ON UPDATE CURRENT_TIMESTAMP

        // FK Navigation
        public Player Player { get; set; } = null!;
    }
}
