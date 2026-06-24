using System.Collections.Concurrent;

namespace PlatformA.Game.Gomoku.Core
{
    /// <summary>
    /// Gomoku 전용 방 관리자. game_transfer 티켓의 string roomId로 방을 조회·생성합니다.
    /// </summary>
    public class GomokuRoomManager
    {
        public static GomokuRoomManager Instance { get; } = new GomokuRoomManager();
        private GomokuRoomManager() { }

        private readonly ConcurrentDictionary<string, GomokuRoom> _rooms = new();

        public GomokuRoom GetOrCreate(string roomId)
        {
            return _rooms.GetOrAdd(roomId, id =>
            {
                Console.WriteLine($"[GomokuRoomManager] 방 생성: {id}");
                return new GomokuRoom(id);
            });
        }

        public GomokuRoom? Find(string roomId)
        {
            _rooms.TryGetValue(roomId, out GomokuRoom? room);
            return room;
        }

        public bool Remove(string roomId)
        {
            if (_rooms.TryRemove(roomId, out _))
            {
                Console.WriteLine($"[GomokuRoomManager] 방 소멸: {roomId}");
                return true;
            }
            return false;
        }
    }
}
