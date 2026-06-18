using PlatformA.Library.Packets;

namespace PlatformA.Game.Gomoku.Core
{
    /// <summary>
    /// 15×15 오목 바둑판. JobQueue 내부에서만 접근되므로 thread-safe 보장 불필요.
    /// </summary>
    public class Board
    {
        public const int Size = 15;

        private readonly StoneColor[,] _cells = new StoneColor[Size, Size];

        /// <summary>지정 좌표에 돌을 놓습니다. 이미 돌이 있거나 범위 밖이면 false를 반환합니다.</summary>
        public bool PlaceStone(int x, int y, StoneColor color)
        {
            if (!IsValid(x, y) || _cells[x, y] != StoneColor.StoneNone)
                return false;

            _cells[x, y] = color;
            return true;
        }

        public StoneColor GetCell(int x, int y) => IsValid(x, y) ? _cells[x, y] : StoneColor.StoneNone;

        public bool IsEmpty(int x, int y) => IsValid(x, y) && _cells[x, y] == StoneColor.StoneNone;

        private static bool IsValid(int x, int y) => x >= 0 && x < Size && y >= 0 && y < Size;
    }
}
