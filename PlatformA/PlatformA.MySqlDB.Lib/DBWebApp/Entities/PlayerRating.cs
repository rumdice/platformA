namespace PlatformA.MySqlDB.Lib.DBWebApp.Entities
{
    /// <summary>
    /// 테이블: player_ratings
    /// 플레이어 ELO 레이팅. 게임 종료 시 Matching.API가 자동 업데이트합니다.
    /// </summary>
    public class PlayerRating
    {
        public int PlayerId { get; set; }       // player_id INT PK (FK → players.id)
        public double Rating { get; set; } = 1000.0;  // rating DOUBLE NOT NULL DEFAULT 1000
        public int WinCount { get; set; }
        public int LoseCount { get; set; }
        public int DrawCount { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Player Player { get; set; } = null!;
    }
}
