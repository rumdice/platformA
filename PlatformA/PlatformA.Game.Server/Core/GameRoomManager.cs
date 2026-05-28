using System.Collections.Concurrent;

namespace PlatformA.Game.Server.Core
{
    /// <summary>
    /// 전체 게임 방의 생성·삭제·조회를 담당하는 Singleton 관리자.
    /// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>로 멀티스레드 접근을 안전하게 처리합니다.
    /// </summary>
    public class GameRoomManager
    {
        public static GameRoomManager Instance { get; } = new GameRoomManager();
        private GameRoomManager() { }


        // 🚨 [주의] 여러 유저가 동시에 방에 입/퇴장 함. 반드시 동시성 제어(Lock)가 필요합니다!
        //private object _lock = new object();
        // 기존 방 관리
        //private Dictionary<int, GameRoom> _rooms = new Dictionary<int, GameRoom>();

        // 🚀 방 번호(int)를 키로 사용하여 여러 방을 안전하게 관리하는 스레드 세이프 딕셔너리
        private ConcurrentDictionary<int, GameRoom> _rooms = new ConcurrentDictionary<int, GameRoom>();

        //private int _roomIdGenerator = 1; // 방 번호 발급기


        // 1. 새로운 방 생성
        //public GameRoom CreateRoom()
        //{
        //    lock (_lock)
        //    {
        //        int roomId = _roomIdGenerator++;
        //        GameRoom room = new GameRoom { RoomId = roomId };
        //        _rooms.Add(roomId, room);
        //        return room;
        //    }
        //}


        /// <summary>
        /// 1. 방 생성 (매칭 서버의 명령을 받을 때 호출됨)
        /// </summary>
        public GameRoom CreateRoom(int _roomId)
        {
            //GameRoom room = new GameRoom();
            //room.RoomId = _roomId; ;

            //if (_rooms.TryAdd(_roomId, room))
            //{
            //    Console.WriteLine($"[RoomManager] 🏠 {_roomId}번 게임 룸이 동적으로 생성되었습니다.");
            //    return room;
            //}

            //// 이미 동일한 번호의 방이 있다면 기존 방을 리턴
            //return _rooms[_roomId];

            //이미 동일한 번호의 방이 있다면 기존 방을 리턴
            return _rooms.GetOrAdd(_roomId, id =>
            {
                Console.WriteLine($"[RoomManager] 🏠 {id}번 게임 룸이 동적으로 생성되었습니다.");
                return new GameRoom { RoomId = id };
            });
        }



        // 2. 방 삭제
        //public bool RemoveRoom(int roomId)
        //{
        //    lock (_lock)
        //    {
        //        return _rooms.Remove(roomId);
        //    }
        //}


        /// <summary>
        /// 방 삭제
        /// </summary>
        public bool RemoveRoom(int roomId)
        {
            if (_rooms.TryRemove(roomId, out _))
            {
                Console.WriteLine($"[RoomManager] 💥 {roomId}번 게임 룸이 소멸되었습니다.");
                return true;
            }
            return false;
        }


        // 3. 방 찾기
        //public GameRoom FindRoom(int roomId)
        //{
        //    lock (_lock)
        //    {
        //        if (_rooms.TryGetValue(roomId, out GameRoom room))
        //            return room;
        //        return null;
        //    }
        //}


        /// <summary>
        /// 3. 방 찾기 (유저가 접속해서 특정 방에 들어갈 때 호출됨) 
        /// </summary>

        public GameRoom FindRoom(int roomId)
        {
            _rooms.TryGetValue(roomId, out GameRoom room);
            return room;
        }
    }
}
