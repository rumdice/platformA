using PlatformA.Game.Gomoku.Core;
using PlatformA.Library.Packets;
using ProtoPacket = PlatformA.Library.Packets.Packet;

namespace PlatformA.Tests.Game.Gomoku
{
    public class GomokuRoomFinishGameGuardTests
    {
        private static ProtoPacket ParseBroadcast(byte[] bytes)
            => ProtoPacket.Parser.ParseFrom(bytes, 2, bytes.Length - 2);

        [Fact]
        public void StartGame_TwoPlayers_BroadcastsSGameStart()
        {
            var room = new TestableGomokuRoom(Guid.NewGuid().ToString());
            var p1 = new FakeGomokuSession(1);
            var p2 = new FakeGomokuSession(2);

            room.Push(() => { room.Enter(p1); room.Enter(p2); });

            var startPkt = room.BroadcastHistory
                .Select(b => ParseBroadcast(b))
                .FirstOrDefault(p => p.PayloadCase == ProtoPacket.PayloadOneofCase.SGameStart);

            Assert.NotNull(startPkt);
            Assert.Equal(p1.SessionId, startPkt!.SGameStart.Player1Id);
            Assert.Equal(p2.SessionId, startPkt!.SGameStart.Player2Id);
        }

        [Fact]
        public void FinishGame_CalledViaTwoPaths_OnlyOneSGameOverBroadcast()
        {
            var room = new TestableGomokuRoom(Guid.NewGuid().ToString());
            var p1 = new FakeGomokuSession(1);
            var p2 = new FakeGomokuSession(2);
            room.Push(() => { room.Enter(p1); room.Enter(p2); });

            // P1 5목으로 게임 종료
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

            // Finished 상태에서 HandleDisconnect 호출 — FinishGame 가드 작동해야 함
            int sGameOverCountBefore = room.BroadcastHistory
                .Count(b => ParseBroadcast(b).PayloadCase == ProtoPacket.PayloadOneofCase.SGameOver);

            room.Push(() => room.HandleDisconnect(p2));

            int sGameOverCountAfter = room.BroadcastHistory
                .Count(b => ParseBroadcast(b).PayloadCase == ProtoPacket.PayloadOneofCase.SGameOver);

            Assert.Equal(sGameOverCountBefore, sGameOverCountAfter);
        }
    }
}
