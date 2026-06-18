using PlatformA.Game.Gomoku.Core;
using PlatformA.Library.Packets;

namespace PlatformA.Tests.Game.Gomoku
{
    public class WinCheckerTests
    {
        // ── 헬퍼: 여러 돌을 한 번에 놓는다 ──────────────────────────────────────
        private static Board PlaceAll(IEnumerable<(int x, int y)> positions, StoneColor color)
        {
            var board = new Board();
            foreach (var (x, y) in positions)
                board.PlaceStone(x, y, color);
            return board;
        }

        // ── 가로 5연속 ───────────────────────────────────────────────────────────
        [Fact]
        public void CheckWin_FiveInRow_Horizontal_ReturnsTrue()
        {
            var positions = Enumerable.Range(0, 5).Select(i => (i, 7));
            var board = PlaceAll(positions, StoneColor.StoneBlack);

            bool result = WinChecker.CheckWin(board, 4, 7, StoneColor.StoneBlack);
            Assert.True(result);
        }

        // ── 세로 5연속 ───────────────────────────────────────────────────────────
        [Fact]
        public void CheckWin_FiveInRow_Vertical_ReturnsTrue()
        {
            var positions = Enumerable.Range(0, 5).Select(i => (7, i));
            var board = PlaceAll(positions, StoneColor.StoneWhite);

            bool result = WinChecker.CheckWin(board, 7, 4, StoneColor.StoneWhite);
            Assert.True(result);
        }

        // ── 주대각선(우하향) 5연속 ──────────────────────────────────────────────
        [Fact]
        public void CheckWin_FiveInRow_DiagonalMain_ReturnsTrue()
        {
            var positions = Enumerable.Range(0, 5).Select(i => (i, i));
            var board = PlaceAll(positions, StoneColor.StoneBlack);

            bool result = WinChecker.CheckWin(board, 4, 4, StoneColor.StoneBlack);
            Assert.True(result);
        }

        // ── 반대각선(우상향) 5연속 ──────────────────────────────────────────────
        [Fact]
        public void CheckWin_FiveInRow_DiagonalAnti_ReturnsTrue()
        {
            // (0,4),(1,3),(2,2),(3,1),(4,0)
            var positions = Enumerable.Range(0, 5).Select(i => (i, 4 - i));
            var board = PlaceAll(positions, StoneColor.StoneWhite);

            bool result = WinChecker.CheckWin(board, 4, 0, StoneColor.StoneWhite);
            Assert.True(result);
        }

        // ── 4연속은 승리 아님 ────────────────────────────────────────────────────
        [Fact]
        public void CheckWin_FourInRow_ReturnsFalse()
        {
            var positions = Enumerable.Range(0, 4).Select(i => (i, 7));
            var board = PlaceAll(positions, StoneColor.StoneBlack);

            bool result = WinChecker.CheckWin(board, 3, 7, StoneColor.StoneBlack);
            Assert.False(result);
        }

        // ── 빈 보드는 승리 없음 ─────────────────────────────────────────────────
        [Fact]
        public void CheckWin_EmptyBoard_ReturnsFalse()
        {
            var board = new Board();
            bool result = WinChecker.CheckWin(board, 7, 7, StoneColor.StoneBlack);
            Assert.False(result);
        }

        // ── 단일 돌은 승리 아님 ─────────────────────────────────────────────────
        [Fact]
        public void CheckWin_SingleStone_ReturnsFalse()
        {
            var board = new Board();
            board.PlaceStone(7, 7, StoneColor.StoneBlack);

            bool result = WinChecker.CheckWin(board, 7, 7, StoneColor.StoneBlack);
            Assert.False(result);
        }

        // ── 다른 색 돌이 끊는 경우 승리 아님 ────────────────────────────────────
        [Fact]
        public void CheckWin_BlockedByOpponent_ReturnsFalse()
        {
            var board = new Board();
            // 흑: (0,7),(1,7),(2,7),(3,7) — 4개
            // 백: (4,7) — 차단
            // 흑: (5,7) — 연속 끊김
            board.PlaceStone(0, 7, StoneColor.StoneBlack);
            board.PlaceStone(1, 7, StoneColor.StoneBlack);
            board.PlaceStone(2, 7, StoneColor.StoneBlack);
            board.PlaceStone(3, 7, StoneColor.StoneBlack);
            board.PlaceStone(4, 7, StoneColor.StoneWhite);
            board.PlaceStone(5, 7, StoneColor.StoneBlack);

            bool result = WinChecker.CheckWin(board, 5, 7, StoneColor.StoneBlack);
            Assert.False(result);
        }

        // ── 정확히 6연속도 승리로 처리됨(count >= 5) ────────────────────────────
        [Fact]
        public void CheckWin_SixInRow_ReturnsTrue()
        {
            var positions = Enumerable.Range(0, 6).Select(i => (i, 7));
            var board = PlaceAll(positions, StoneColor.StoneBlack);

            bool result = WinChecker.CheckWin(board, 5, 7, StoneColor.StoneBlack);
            Assert.True(result);
        }

        // ── 보드 경계 근처에서도 정상 동작 ─────────────────────────────────────
        [Fact]
        public void CheckWin_NearBoardEdge_ReturnsTrue()
        {
            // 가로: (10,14),(11,14),(12,14),(13,14),(14,14)
            var positions = Enumerable.Range(10, 5).Select(i => (i, 14));
            var board = PlaceAll(positions, StoneColor.StoneWhite);

            bool result = WinChecker.CheckWin(board, 14, 14, StoneColor.StoneWhite);
            Assert.True(result);
        }
    }
}
