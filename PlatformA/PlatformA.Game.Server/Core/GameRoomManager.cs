using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlatformA.Game.Server.Core
{
    public class GameRoomManager
    {
        public static GameRoomManager Instance { get; } = new GameRoomManager();
        private GameRoomManager() { }

        // 🚨 [주의] 여러 유저가 동시에 방에 입/퇴장 함. 반드시 동시성 제어(Lock)가 필요합니다!
        private object _lock = new object();
        private Dictionary<int, GameRoom> _rooms = new Dictionary<int, GameRoom>();
        private int _roomIdGenerator = 1; // 방 번호 발급기


        // 1. 새로운 방 생성
        public GameRoom CreateRoom()
        {
            lock (_lock)
            {
                int roomId = _roomIdGenerator++;
                GameRoom room = new GameRoom { RoomId = roomId };
                _rooms.Add(roomId, room);
                return room;
            }
        }

        // 2. 방 삭제
        public bool RemoveRoom(int roomId)
        {
            lock (_lock)
            {
                return _rooms.Remove(roomId);
            }
        }

        // 3. 방 찾기
        public GameRoom FindRoom(int roomId)
        {
            lock (_lock)
            {
                if (_rooms.TryGetValue(roomId, out GameRoom room))
                    return room;
                return null;
            }
        }
    }
}
