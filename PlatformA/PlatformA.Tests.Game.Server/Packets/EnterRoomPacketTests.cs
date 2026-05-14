using Google.Protobuf;
using PlatformA.Library.Packets;

namespace PlatformA.Tests.Game.Server.Packets
{
    public class EnterRoomPacketTests
    {
        [Fact]
        public void CEnterRoom_RoundTrip_ExtractsRoomId()
        {
            var original = new CEnterRoom { RoomId = 7 };
            var result = CEnterRoom.Parser.ParseFrom(original.ToByteArray());

            Assert.Equal(7, result.RoomId);
        }

        [Fact]
        public void SEnterRoom_Success_RoundTrip()
        {
            var original = new SEnterRoom { ResultCode = EnterRoomResultCode.EnterRoomSuccess, RoomId = 5 };
            var result = SEnterRoom.Parser.ParseFrom(original.ToByteArray());

            Assert.Equal(EnterRoomResultCode.EnterRoomSuccess, result.ResultCode);
            Assert.Equal(5, result.RoomId);
        }

        [Fact]
        public void SEnterRoom_NotFound_RoundTrip()
        {
            var original = new SEnterRoom { ResultCode = EnterRoomResultCode.EnterRoomNotFound, RoomId = 0 };
            var result = SEnterRoom.Parser.ParseFrom(original.ToByteArray());

            Assert.Equal(EnterRoomResultCode.EnterRoomNotFound, result.ResultCode);
        }

    }
}
