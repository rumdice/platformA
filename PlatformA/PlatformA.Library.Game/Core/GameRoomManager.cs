using System.Collections.Concurrent;

namespace PlatformA.Library.Game.Core
{
    /// <summary>
    /// 전체 게임 방의 생성·삭제·조회를 담당하는 Singleton 관리자.
    /// </summary>
    public class GameRoomManager
    {
        public static GameRoomManager Instance { get; } = new GameRoomManager();
        private GameRoomManager() { }

        private ConcurrentDictionary<int, GameRoom> _rooms = new ConcurrentDictionary<int, GameRoom>();

        /// <summary>방 생성. 이미 동일한 번호의 방이 있으면 기존 방을 반환합니다.</summary>
        public GameRoom CreateRoom(int roomId)
        {
            return _rooms.GetOrAdd(roomId, id =>
            {
                Console.WriteLine($"[RoomManager] {id}번 게임 룸이 생성되었습니다.");
                return new GameRoom { RoomId = id };
            });
        }

        /// <summary>방 삭제.</summary>
        public bool RemoveRoom(int roomId)
        {
            if (_rooms.TryRemove(roomId, out _))
            {
                Console.WriteLine($"[RoomManager] {roomId}번 게임 룸이 소멸되었습니다.");
                return true;
            }
            return false;
        }

        /// <summary>방 조회. 없으면 null 반환.</summary>
        public GameRoom? FindRoom(int roomId)
        {
            _rooms.TryGetValue(roomId, out GameRoom? room);
            return room;
        }
    }
}
