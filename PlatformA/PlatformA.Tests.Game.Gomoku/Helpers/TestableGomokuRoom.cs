using PlatformA.Game.Gomoku.Core;

namespace PlatformA.Tests.Game.Gomoku
{
    /// <summary>
    /// Broadcast()를 override하여 TCP 전송 없이 패킷을 캡처하는 테스트용 GomokuRoom.
    /// BroadcastHistory로 송신된 패킷 내용을 검증할 수 있다.
    /// </summary>
    internal class TestableGomokuRoom : GomokuRoom
    {
        public List<byte[]> BroadcastHistory { get; } = new();

        public TestableGomokuRoom(string roomId) : base(roomId) { }

        public override void Broadcast(byte[] packet) => BroadcastHistory.Add(packet);
    }
}
