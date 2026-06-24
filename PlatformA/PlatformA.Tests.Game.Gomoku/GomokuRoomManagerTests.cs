using PlatformA.Game.Gomoku.Core;

namespace PlatformA.Tests.Game.Gomoku
{
    public class GomokuRoomManagerTests
    {
        // 각 테스트에서 Guid 기반의 고유 roomId를 사용하여 싱글톤 간섭을 방지한다.

        [Fact]
        public void GetOrCreate_SameRoomId_ReturnsSameInstance()
        {
            string roomId = Guid.NewGuid().ToString();

            GomokuRoom first = GomokuRoomManager.Instance.GetOrCreate(roomId);
            GomokuRoom second = GomokuRoomManager.Instance.GetOrCreate(roomId);

            Assert.Same(first, second);

            // 정리
            GomokuRoomManager.Instance.Remove(roomId);
        }

        [Fact]
        public void GetOrCreate_DifferentRoomIds_ReturnsDifferentInstances()
        {
            string roomId1 = Guid.NewGuid().ToString();
            string roomId2 = Guid.NewGuid().ToString();

            GomokuRoom room1 = GomokuRoomManager.Instance.GetOrCreate(roomId1);
            GomokuRoom room2 = GomokuRoomManager.Instance.GetOrCreate(roomId2);

            Assert.NotSame(room1, room2);

            // 정리
            GomokuRoomManager.Instance.Remove(roomId1);
            GomokuRoomManager.Instance.Remove(roomId2);
        }

        [Fact]
        public void Remove_AfterGetOrCreate_FindReturnsNull()
        {
            string roomId = Guid.NewGuid().ToString();

            GomokuRoomManager.Instance.GetOrCreate(roomId);
            bool removed = GomokuRoomManager.Instance.Remove(roomId);

            Assert.True(removed);
            Assert.Null(GomokuRoomManager.Instance.Find(roomId));
        }

        [Fact]
        public void Remove_NonExistentRoomId_ReturnsFalse()
        {
            string roomId = Guid.NewGuid().ToString();

            bool removed = GomokuRoomManager.Instance.Remove(roomId);

            Assert.False(removed);
        }

        [Fact]
        public void Find_ExistingRoomId_ReturnsRoom()
        {
            string roomId = Guid.NewGuid().ToString();

            GomokuRoom created = GomokuRoomManager.Instance.GetOrCreate(roomId);
            GomokuRoom? found = GomokuRoomManager.Instance.Find(roomId);

            Assert.NotNull(found);
            Assert.Same(created, found);

            // 정리
            GomokuRoomManager.Instance.Remove(roomId);
        }

        [Fact]
        public void Find_NonExistentRoomId_ReturnsNull()
        {
            string roomId = Guid.NewGuid().ToString();

            GomokuRoom? found = GomokuRoomManager.Instance.Find(roomId);

            Assert.Null(found);
        }
    }
}
