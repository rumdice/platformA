using PlatformA.Game.Gomoku.Core;
using PlatformA.Library.Packets;

namespace PlatformA.Tests.Game.Gomoku
{
    public class BoardTests
    {
        [Fact]
        public void PlaceStone_ValidPosition_ReturnsTrue()
        {
            var board = new Board();
            bool result = board.PlaceStone(0, 0, StoneColor.StoneBlack);
            Assert.True(result);
        }

        [Fact]
        public void PlaceStone_OccupiedPosition_ReturnsFalse()
        {
            var board = new Board();
            board.PlaceStone(7, 7, StoneColor.StoneBlack);
            bool result = board.PlaceStone(7, 7, StoneColor.StoneWhite);
            Assert.False(result);
        }

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(0, -1)]
        [InlineData(15, 0)]
        [InlineData(0, 15)]
        [InlineData(-1, -1)]
        [InlineData(15, 15)]
        public void PlaceStone_OutOfBounds_ReturnsFalse(int x, int y)
        {
            var board = new Board();
            bool result = board.PlaceStone(x, y, StoneColor.StoneBlack);
            Assert.False(result);
        }

        [Fact]
        public void GetCell_AfterPlaceStone_ReturnsCorrectColor()
        {
            var board = new Board();
            board.PlaceStone(3, 5, StoneColor.StoneBlack);
            board.PlaceStone(4, 6, StoneColor.StoneWhite);

            Assert.Equal(StoneColor.StoneBlack, board.GetCell(3, 5));
            Assert.Equal(StoneColor.StoneWhite, board.GetCell(4, 6));
        }

        [Fact]
        public void GetCell_OutOfBounds_ReturnsStoneNone()
        {
            var board = new Board();
            Assert.Equal(StoneColor.StoneNone, board.GetCell(-1, 0));
            Assert.Equal(StoneColor.StoneNone, board.GetCell(0, 15));
        }

        [Fact]
        public void IsEmpty_NewBoard_ReturnsTrue()
        {
            var board = new Board();
            for (int x = 0; x < Board.Size; x++)
                for (int y = 0; y < Board.Size; y++)
                    Assert.True(board.IsEmpty(x, y));
        }

        [Fact]
        public void IsEmpty_AfterPlaceStone_ReturnsFalse()
        {
            var board = new Board();
            board.PlaceStone(7, 7, StoneColor.StoneBlack);
            Assert.False(board.IsEmpty(7, 7));
        }

        [Fact]
        public void PlaceStone_AllCornersAndCenter_ReturnsTrue()
        {
            var board = new Board();
            Assert.True(board.PlaceStone(0, 0, StoneColor.StoneBlack));
            Assert.True(board.PlaceStone(14, 0, StoneColor.StoneWhite));
            Assert.True(board.PlaceStone(0, 14, StoneColor.StoneBlack));
            Assert.True(board.PlaceStone(14, 14, StoneColor.StoneWhite));
            Assert.True(board.PlaceStone(7, 7, StoneColor.StoneBlack));
        }

        [Fact]
        public void IsEmpty_OutOfBounds_ReturnsFalse()
        {
            var board = new Board();
            Assert.False(board.IsEmpty(-1, 0));
            Assert.False(board.IsEmpty(0, 15));
        }

        // ── IsFull ───────────────────────────────────────────────────────────

        [Fact]
        public void IsFull_EmptyBoard_ReturnsFalse()
        {
            var board = new Board();
            Assert.False(board.IsFull());
        }

        [Fact]
        public void IsFull_PartialBoard_ReturnsFalse()
        {
            var board = new Board();
            // 일부 칸만 채움 — 전체 225칸 중 몇 칸
            board.PlaceStone(0, 0, StoneColor.StoneBlack);
            board.PlaceStone(1, 0, StoneColor.StoneWhite);
            board.PlaceStone(2, 0, StoneColor.StoneBlack);
            Assert.False(board.IsFull());
        }

        [Fact]
        public void IsFull_FullBoard_ReturnsTrue()
        {
            var board = new Board();
            // 15×15 = 225칸 전부 채움 (흑백 교대)
            for (int x = 0; x < Board.Size; x++)
            {
                for (int y = 0; y < Board.Size; y++)
                {
                    StoneColor color = (x + y) % 2 == 0
                        ? StoneColor.StoneBlack
                        : StoneColor.StoneWhite;
                    board.PlaceStone(x, y, color);
                }
            }
            Assert.True(board.IsFull());
        }
    }
}
