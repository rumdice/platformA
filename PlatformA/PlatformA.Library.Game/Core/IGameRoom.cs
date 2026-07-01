using PlatformA.Library.Game.Network;

namespace PlatformA.Library.Game.Core
{
    public interface IGameRoom
    {
        int RoomId { get; set; }
        void Push(Action job);
        void Enter(GameSession session);
        void Leave(GameSession session);
        void Broadcast(byte[] packet);
        IReadOnlyList<GameSession> Sessions { get; }
    }
}
