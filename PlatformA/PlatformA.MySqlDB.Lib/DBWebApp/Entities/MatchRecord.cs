namespace PlatformA.MySqlDB.Lib.DBWebApp.Entities
{
    /// <summary>
    /// match_records.status 컬럼 값
    /// </summary>
    public enum MatchStatus : byte
    {
        Pending    = 0, // 매칭 대기 중
        InProgress = 1, // 게임 진행 중
        Completed  = 2, // 정상 종료
        Cancelled  = 3  // 취소 / 비정상 종료
    }

    /// <summary>
    /// 테이블: match_records
    /// 매칭 이력. Matching.API 와 Game.Server 가 공유합니다.
    /// </summary>
    public class MatchRecord
    {
        public long Id { get; set; }             // id BIGINT AUTO_INCREMENT PK
        public int Player1Id { get; set; }       // player1_id INT NOT NULL (FK → players.id)
        public int Player2Id { get; set; }       // player2_id INT NOT NULL (FK → players.id)
        public int? WinnerId { get; set; }        // winner_id INT NULL (FK → players.id)
        public MatchStatus Status { get; set; }  // status TINYINT NOT NULL DEFAULT 0
        public DateTime? StartedAt { get; set; } // started_at DATETIME(6) NULL
        public DateTime? EndedAt { get; set; }   // ended_at DATETIME(6) NULL
        public DateTime CreatedAt { get; set; }  // created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP

        // FK Navigation
        public Player Player1 { get; set; } = null!;
        public Player Player2 { get; set; } = null!;
        public Player? Winner { get; set; }
    }
}
