using PlatformA.Game.Gomoku.Core;
using PlatformA.Library.Packets;
using ProtoPacket = PlatformA.Library.Packets.Packet;

namespace PlatformA.Tests.Game.Gomoku
{
    public class GomokuRoomDisconnectTests
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
        public void HandleDisconnect_WaitingPlayers_BroadcastsSGameOver()
        {
            var room = new TestableGomokuRoom(Guid.NewGuid().ToString());
            var p1 = new FakeGomokuSession(1);
            room.Push(() => room.Enter(p1));

            int broadcastsBefore = room.BroadcastHistory.Count;
            room.Push(() => room.HandleDisconnect(p1));

            Assert.Equal(broadcastsBefore + 1, room.BroadcastHistory.Count);
            var pkt = ParseBroadcast(room.BroadcastHistory[^1]);
            Assert.Equal(ProtoPacket.PayloadOneofCase.SGameOver, pkt.PayloadCase);
        }

        [Fact]
        public void HandleDisconnect_WaitingPlayers_GameStateStaysWaiting()
        {
            var room = new TestableGomokuRoom(Guid.NewGuid().ToString());
            var p1 = new FakeGomokuSession(1);
            room.Push(() => room.Enter(p1));

            room.Push(() => room.HandleDisconnect(p1));

            Assert.Equal(GomokuGameState.WaitingPlayers, room.GameState);
        }

        [Fact]
        public void HandleDisconnect_InProgress_GameStateBecomesFinished()
        {
            var room = SetupInProgress(out var p1, out _);

            room.Push(() => room.HandleDisconnect(p1));

            Assert.Equal(GomokuGameState.Finished, room.GameState);
        }

        [Fact]
        public void HandleDisconnect_InProgress_OpponentWins_BroadcastWinnerId()
        {
            var room = SetupInProgress(out var p1, out _);

            room.Push(() => room.HandleDisconnect(p1));

            var gameOverPkt = room.BroadcastHistory
                .Select(b => ParseBroadcast(b))
                .Last(p => p.PayloadCase == ProtoPacket.PayloadOneofCase.SGameOver);

            // p1(SessionId=1)이 끊겼으므로 상대방 p2(SessionId=2)가 승자
            Assert.Equal(2, gameOverPkt.SGameOver.WinnerId);
        }

        [Fact]
        public void HandleDisconnect_Finished_NoAdditionalBroadcast()
        {
            var room = SetupInProgress(out var p1, out _);

            // 게임 종료 (p1 끊김)
            room.Push(() => room.HandleDisconnect(p1));
            Assert.Equal(GomokuGameState.Finished, room.GameState);

            int broadcastsAfterFinish = room.BroadcastHistory.Count;

            // Finished 상태에서 한 번 더 HandleDisconnect 호출
            var p2 = new FakeGomokuSession(2);
            room.Push(() => room.HandleDisconnect(p2));

            // 가드가 작동하여 추가 브로드캐스트 없음
            Assert.Equal(broadcastsAfterFinish, room.BroadcastHistory.Count);
        }
    }
}
