namespace PlatformA.Game.Gomoku.Core
{
    /// <summary>
    /// 두 플레이어의 턴 교대와 타임아웃을 관리합니다. GameRoom의 JobQueue 내부에서만 접근됩니다.
    /// </summary>
    public class TurnManager
    {
        public const int TurnTimeoutSeconds = 30;

        public int Player1Id { get; }
        public int Player2Id { get; }
        public int CurrentTurnPlayerId { get; private set; }

        private DateTime _turnStartedAt;

        public TurnManager(int player1Id, int player2Id)
        {
            Player1Id = player1Id;
            Player2Id = player2Id;
            CurrentTurnPlayerId = player1Id; // 흑(Player1)이 선공
            _turnStartedAt = DateTime.UtcNow;
        }

        /// <summary>현재 차례 플레이어가 맞는지 확인합니다.</summary>
        public bool IsCurrentTurn(int playerId) => CurrentTurnPlayerId == playerId;

        /// <summary>턴을 다음 플레이어로 넘깁니다.</summary>
        public void NextTurn()
        {
            CurrentTurnPlayerId = CurrentTurnPlayerId == Player1Id ? Player2Id : Player1Id;
            _turnStartedAt = DateTime.UtcNow;
        }

        /// <summary>현재 턴 플레이어가 타임아웃을 초과했는지 확인합니다.</summary>
        public bool IsTimeout() =>
            (DateTime.UtcNow - _turnStartedAt).TotalSeconds > TurnTimeoutSeconds;

        /// <summary>타임아웃된 경우 상대방 ID를 반환합니다.</summary>
        public int GetOpponentId(int playerId) =>
            playerId == Player1Id ? Player2Id : Player1Id;
    }
}
