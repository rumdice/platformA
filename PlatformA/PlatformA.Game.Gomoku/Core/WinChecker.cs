using PlatformA.Library.Packets;

namespace PlatformA.Game.Gomoku.Core
{
    /// <summary>
    /// 마지막으로 놓인 돌 위치에서 5연속 승리 조건을 검사합니다.
    /// </summary>
    public static class WinChecker
    {
        private static readonly (int dx, int dy)[] Directions =
        [
            (1, 0),   // 가로
            (0, 1),   // 세로
            (1, 1),   // 우하향 대각선
            (1, -1),  // 우상향 대각선
        ];

        /// <summary>
        /// lastX, lastY에 놓인 color 돌이 5연속을 완성했는지 확인합니다.
        /// </summary>
        public static bool CheckWin(Board board, int lastX, int lastY, StoneColor color)
        {
            foreach (var (dx, dy) in Directions)
            {
                int count = 1
                    + CountDirection(board, lastX, lastY, dx, dy, color)
                    + CountDirection(board, lastX, lastY, -dx, -dy, color);

                if (count >= 5)
                    return true;
            }
            return false;
        }

        private static int CountDirection(Board board, int x, int y, int dx, int dy, StoneColor color)
        {
            int count = 0;
            for (int i = 1; i < 5; i++)
            {
                int nx = x + dx * i;
                int ny = y + dy * i;
                if (board.GetCell(nx, ny) == color)
                    count++;
                else
                    break;
            }
            return count;
        }
    }
}
