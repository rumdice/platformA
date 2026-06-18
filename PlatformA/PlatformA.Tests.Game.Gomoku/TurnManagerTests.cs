using PlatformA.Game.Gomoku.Core;

namespace PlatformA.Tests.Game.Gomoku
{
    public class TurnManagerTests
    {
        private const int Player1Id = 1001;
        private const int Player2Id = 2002;

        // ── 생성자: Player1이 첫 턴 ─────────────────────────────────────────────
        [Fact]
        public void Constructor_Player1StartsFirst()
        {
            var tm = new TurnManager(Player1Id, Player2Id);
            Assert.Equal(Player1Id, tm.CurrentTurnPlayerId);
        }

        [Fact]
        public void Constructor_SetsPlayerIds()
        {
            var tm = new TurnManager(Player1Id, Player2Id);
            Assert.Equal(Player1Id, tm.Player1Id);
            Assert.Equal(Player2Id, tm.Player2Id);
        }

        // ── NextTurn: P1 → P2 전환 ───────────────────────────────────────────────
        [Fact]
        public void NextTurn_SwitchesTurn()
        {
            var tm = new TurnManager(Player1Id, Player2Id);
            tm.NextTurn();
            Assert.Equal(Player2Id, tm.CurrentTurnPlayerId);
        }

        // ── NextTurn: P2 → P1 전환 (두 번 호출) ─────────────────────────────────
        [Fact]
        public void NextTurn_CalledTwice_ReturnsTurnToPlayer1()
        {
            var tm = new TurnManager(Player1Id, Player2Id);
            tm.NextTurn();
            tm.NextTurn();
            Assert.Equal(Player1Id, tm.CurrentTurnPlayerId);
        }

        // ── IsCurrentTurn ────────────────────────────────────────────────────────
        [Fact]
        public void IsCurrentTurn_CurrentPlayer_ReturnsTrue()
        {
            var tm = new TurnManager(Player1Id, Player2Id);
            Assert.True(tm.IsCurrentTurn(Player1Id));
        }

        [Fact]
        public void IsCurrentTurn_OtherPlayer_ReturnsFalse()
        {
            var tm = new TurnManager(Player1Id, Player2Id);
            Assert.False(tm.IsCurrentTurn(Player2Id));
        }

        [Fact]
        public void IsCurrentTurn_AfterNextTurn_UpdatesCorrectly()
        {
            var tm = new TurnManager(Player1Id, Player2Id);
            tm.NextTurn();
            Assert.False(tm.IsCurrentTurn(Player1Id));
            Assert.True(tm.IsCurrentTurn(Player2Id));
        }

        // ── GetOpponentId ────────────────────────────────────────────────────────
        [Fact]
        public void GetOpponentId_Player1_ReturnsPlayer2Id()
        {
            var tm = new TurnManager(Player1Id, Player2Id);
            Assert.Equal(Player2Id, tm.GetOpponentId(Player1Id));
        }

        [Fact]
        public void GetOpponentId_Player2_ReturnsPlayer1Id()
        {
            var tm = new TurnManager(Player1Id, Player2Id);
            Assert.Equal(Player1Id, tm.GetOpponentId(Player2Id));
        }

        // ── IsTimeout ────────────────────────────────────────────────────────────
        [Fact]
        public void IsTimeout_WithinLimit_ReturnsFalse()
        {
            var tm = new TurnManager(Player1Id, Player2Id);
            // 방금 생성된 TurnManager는 타임아웃(30초) 훨씬 이내
            Assert.False(tm.IsTimeout());
        }

        [Fact]
        public void IsTimeout_AfterNextTurn_ResetsClock()
        {
            var tm = new TurnManager(Player1Id, Player2Id);
            tm.NextTurn();
            // NextTurn() 직후에도 타임아웃 이내여야 함
            Assert.False(tm.IsTimeout());
        }

        // ── TurnTimeoutSeconds 상수 확인 ─────────────────────────────────────────
        [Fact]
        public void TurnTimeoutSeconds_Is30()
        {
            Assert.Equal(30, TurnManager.TurnTimeoutSeconds);
        }
    }
}
