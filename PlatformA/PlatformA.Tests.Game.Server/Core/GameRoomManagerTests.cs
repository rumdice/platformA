#nullable disable
using PlatformA.Library.Game.Core;

namespace PlatformA.Tests.Game.Server.Core
{
    public class GameRoomManagerTests
    {
        // 다른 테스트나 실제 서버와 충돌하지 않도록 높은 ID 대역을 사용한다.
        private const int BaseRoomId = 900000;
        private readonly GameRoomManager _manager = GameRoomManager.Instance;

        [Fact]
        public void CreateRoom_NewRoom_ReturnsRoomWithCorrectId()
        {
            var room = _manager.CreateRoom(BaseRoomId + 1);

            Assert.NotNull(room);
            Assert.Equal(BaseRoomId + 1, room.RoomId);
        }

        [Fact]
        public void CreateRoom_SameId_ReturnsSameInstance()
        {
            var room1 = _manager.CreateRoom(BaseRoomId + 2);
            var room2 = _manager.CreateRoom(BaseRoomId + 2);

            Assert.Same(room1, room2);
        }

        [Fact]
        public void FindRoom_ExistingRoom_ReturnsRoom()
        {
            _manager.CreateRoom(BaseRoomId + 3);

            var found = _manager.FindRoom(BaseRoomId + 3);

            Assert.NotNull(found);
            Assert.Equal(BaseRoomId + 3, found.RoomId);
        }

        [Fact]
        public void FindRoom_NonExistentRoom_ReturnsNull()
        {
            var found = _manager.FindRoom(BaseRoomId + 4);

            Assert.Null(found);
        }

        [Fact]
        public void RemoveRoom_ExistingRoom_RoomNoLongerFound()
        {
            _manager.CreateRoom(BaseRoomId + 5);
            _manager.RemoveRoom(BaseRoomId + 5);

            var found = _manager.FindRoom(BaseRoomId + 5);

            Assert.Null(found);
        }

        [Fact]
        public void RemoveRoom_NonExistent_ReturnsFalse()
        {
            var result = _manager.RemoveRoom(BaseRoomId + 6);

            Assert.False(result);
        }

        [Fact]
        public void CreateRoom_ConcurrentAccess_ThreadSafe()
        {
            const int threadCount = 100;
            const int targetRoomId = BaseRoomId + 7;
            var results = new GameRoom[threadCount];
            var threads = new Thread[threadCount];

            for (int i = 0; i < threadCount; i++)
            {
                int idx = i;
                threads[idx] = new Thread(() => results[idx] = _manager.CreateRoom(targetRoomId));
            }

            foreach (var t in threads)
                t.Start();
            foreach (var t in threads)
                t.Join();

            for (int i = 1; i < threadCount; i++)
                Assert.Same(results[0], results[i]);
        }
    }
}
