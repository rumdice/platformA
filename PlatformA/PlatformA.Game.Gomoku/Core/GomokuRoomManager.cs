using PlatformA.Library.Game.Core;

namespace PlatformA.Game.Gomoku.Core
{
    public class GomokuRoomManager : GameRoomManagerBase<GomokuRoom, string>
    {
        public static GomokuRoomManager Instance { get; } = new GomokuRoomManager();
        private GomokuRoomManager() { }

        public new GomokuRoom GetOrCreate(string roomId)
        {
            return GetOrCreate(roomId, id =>
            {
                Console.WriteLine($"[GomokuRoomManager] 방 생성: {id}");
                return new GomokuRoom(id);
            });
        }
    }
}
