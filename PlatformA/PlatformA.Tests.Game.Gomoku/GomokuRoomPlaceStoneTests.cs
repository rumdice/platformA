using System.Reflection;
using PlatformA.Game.Gomoku.Core;
using PlatformA.Library.Packets;
using ProtoPacket = PlatformA.Library.Packets.Packet;

namespace PlatformA.Tests.Game.Gomoku
{
    public class GomokuRoomPlaceStoneTests
    {
        private static ProtoPacket ParseBroadcast(byte[] bytes)
            => ProtoPacket.Parser.ParseFrom(bytes, 2, bytes.Length - 2);

        private static TestableGomokuRoom SetupInProgress(
            out FakeGomokuSession p1,
            out FakeGomokuSession p2)
        {
            var room = new TestableGomokuRoom(Guid.NewGuid().ToString());
            var localP1 = new FakeGomokuSession(1);
            var localP2 = new FakeGomokuSession(2);
            room.Push(() => { room.Enter(localP1); room.Enter(localP2); });
            p1 = localP1;
            p2 = localP2;
            return room;
        }

        [Fact]
        public void HandlePlaceStone_BeforeGameStart_Ignored()
        {
            var room = new TestableGomokuRoom(Guid.NewGuid().ToString());
            var p1 = new FakeGomokuSession(1);
            room.Push(() => room.Enter(p1));

            room.Push(() => room.HandlePlaceStone(p1, 7, 7));

            // WaitingPlayers 상태에서는 조기 반환 — 브로드캐스트 없음
            Assert.DoesNotContain(room.BroadcastHistory,
                b => ParseBroadcast(b).PayloadCase == ProtoPacket.PayloadOneofCase.SBoardUpdate);
        }

        [Fact]
        public void HandlePlaceStone_NotCurrentTurn_Rejected()
        {
            var room = SetupInProgress(out _, out var p2);

            int before = room.BroadcastHistory.Count;

            // P1 선공인데 P2가 먼저 돌을 놓으려 함
            room.Push(() => room.HandlePlaceStone(p2, 7, 7));

            Assert.Equal(before, room.BroadcastHistory.Count);
        }

        [Fact]
        public void HandlePlaceStone_OccupiedCell_Rejected()
        {
            var room = SetupInProgress(out var p1, out var p2);

            // P1 정상 수
            room.Push(() => room.HandlePlaceStone(p1, 7, 7));
            int before = room.BroadcastHistory.Count;

            // P2 → P1 차례 복귀를 위해 P2도 두고
            room.Push(() => room.HandlePlaceStone(p2, 0, 0));
            // 다시 P1이 이미 돌 있는 (7,7)에 시도
            before = room.BroadcastHistory.Count;
            room.Push(() => room.HandlePlaceStone(p1, 7, 7));

            Assert.Equal(before, room.BroadcastHistory.Count);
        }

        [Fact]
        public void HandlePlaceStone_ValidMove_BroadcastsSBoardUpdate()
        {
            var room = SetupInProgress(out var p1, out _);

            room.Push(() => room.HandlePlaceStone(p1, 7, 7));

            var pkt = room.BroadcastHistory
                .Select(b => ParseBroadcast(b))
                .LastOrDefault(p => p.PayloadCase == ProtoPacket.PayloadOneofCase.SBoardUpdate);

            Assert.NotNull(pkt);
            Assert.Equal(7, pkt!.SBoardUpdate.X);
            Assert.Equal(7, pkt!.SBoardUpdate.Y);
            Assert.Equal(StoneColor.StoneBlack, pkt!.SBoardUpdate.Color);
        }

        [Fact]
        public void HandlePlaceStone_ValidMove_TurnSwitches()
        {
            var room = SetupInProgress(out var p1, out var p2);

            // P1이 정상 수 후 SBoardUpdate.NextTurnPlayerId == P2
            room.Push(() => room.HandlePlaceStone(p1, 7, 7));

            var pkt = room.BroadcastHistory
                .Select(b => ParseBroadcast(b))
                .Last(p => p.PayloadCase == ProtoPacket.PayloadOneofCase.SBoardUpdate);

            Assert.Equal(p2.SessionId, pkt.SBoardUpdate.NextTurnPlayerId);
        }

        [Fact]
        public void HandlePlaceStone_FiveInRow_GameFinished()
        {
            var room = SetupInProgress(out var p1, out var p2);

            // P1 흑 가로 5연속: (7,7)(8,7)(9,7)(10,7)(11,7)
            // P2 방해 수: (0,0)(1,0)(2,0)(3,0)
            room.Push(() => room.HandlePlaceStone(p1, 7, 7));
            room.Push(() => room.HandlePlaceStone(p2, 0, 0));
            room.Push(() => room.HandlePlaceStone(p1, 8, 7));
            room.Push(() => room.HandlePlaceStone(p2, 1, 0));
            room.Push(() => room.HandlePlaceStone(p1, 9, 7));
            room.Push(() => room.HandlePlaceStone(p2, 2, 0));
            room.Push(() => room.HandlePlaceStone(p1, 10, 7));
            room.Push(() => room.HandlePlaceStone(p2, 3, 0));
            room.Push(() => room.HandlePlaceStone(p1, 11, 7));

            Assert.Equal(GomokuGameState.Finished, room.GameState);
        }

        [Fact]
        public void HandlePlaceStone_FiveInRow_WinnerIdCorrect()
        {
            var room = SetupInProgress(out var p1, out var p2);

            room.Push(() => room.HandlePlaceStone(p1, 7, 7));
            room.Push(() => room.HandlePlaceStone(p2, 0, 0));
            room.Push(() => room.HandlePlaceStone(p1, 8, 7));
            room.Push(() => room.HandlePlaceStone(p2, 1, 0));
            room.Push(() => room.HandlePlaceStone(p1, 9, 7));
            room.Push(() => room.HandlePlaceStone(p2, 2, 0));
            room.Push(() => room.HandlePlaceStone(p1, 10, 7));
            room.Push(() => room.HandlePlaceStone(p2, 3, 0));
            room.Push(() => room.HandlePlaceStone(p1, 11, 7));

            var gameOverPkt = room.BroadcastHistory
                .Select(b => ParseBroadcast(b))
                .Last(p => p.PayloadCase == ProtoPacket.PayloadOneofCase.SGameOver);

            Assert.Equal(p1.SessionId, gameOverPkt.SGameOver.WinnerId);
        }

        [Fact]
        public void HandlePlaceStone_FullBoard_DrawResult()
        {
            var room = SetupInProgress(out var p1, out _);

            // _board에 5목이 되지 않는 패턴으로 224개 사전 채우기
            // phase = (x%4 + 2*(y%4)) % 4; Black if phase < 2, else White
            // 최대 연속: 가로/세로/대각 모두 2 이하 — 검증됨
            var boardField = typeof(GomokuRoom)
                .GetField("_board", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var board = (Board)boardField.GetValue(room)!;

            // (0,0)을 비워 두고 나머지 224개 채우기
            for (int x = 0; x < Board.Size; x++)
            {
                for (int y = 0; y < Board.Size; y++)
                {
                    if (x == 0 && y == 0)
                        continue;
                    int phase = (x % 4 + 2 * (y % 4)) % 4;
                    StoneColor color = phase < 2 ? StoneColor.StoneBlack : StoneColor.StoneWhite;
                    board.PlaceStone(x, y, color);
                }
            }

            // P1(흑)이 마지막 빈 칸 (0,0)에 돌을 놓아 IsFull() → Draw
            room.Push(() => room.HandlePlaceStone(p1, 0, 0));

            var gameOverPkt = room.BroadcastHistory
                .Select(b => ParseBroadcast(b))
                .Last(p => p.PayloadCase == ProtoPacket.PayloadOneofCase.SGameOver);

            Assert.Equal(GomokuGameState.Finished, room.GameState);
            Assert.Equal(0, gameOverPkt.SGameOver.WinnerId);
        }
    }
}
