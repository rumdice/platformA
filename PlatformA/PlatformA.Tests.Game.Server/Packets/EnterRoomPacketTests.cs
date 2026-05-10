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

        [Fact]
        public void PacketID_EnumValues_AreCorrect()
        {
            Assert.Equal((ushort)1, (ushort)PacketID.C_Move);
            Assert.Equal((ushort)2, (ushort)PacketID.S_Move);
            Assert.Equal((ushort)3, (ushort)PacketID.C_Login);
            Assert.Equal((ushort)4, (ushort)PacketID.S_Login);
            Assert.Equal((ushort)5, (ushort)PacketID.C_EnterRoom);
            Assert.Equal((ushort)6, (ushort)PacketID.S_EnterRoom);
        }
    }
}
