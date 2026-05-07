using System.Buffers.Binary;
using PlatformA.Library.Packets;

namespace PlatformA.Tests.Game.Server.Packets
{
    public class EnterRoomPacketTests
    {
        [Fact]
        public void CEnterRoomPacket_Deserialize_ExtractsRoomId()
        {
            byte[] buf = new byte[C_EnterRoomPacket.Size];
            BinaryPrimitives.WriteInt32LittleEndian(buf, 7);

            var packet = new C_EnterRoomPacket();
            packet.Deserialize(buf);

            Assert.Equal(7, packet.RoomId);
        }

        [Fact]
        public void CEnterRoomPacket_Deserialize_CorrectByteLength()
        {
            Assert.Equal(4, C_EnterRoomPacket.Size);
        }

        [Fact]
        public void SEnterRoomPacket_Serialize_ProducesCorrectByteLength()
        {
            Assert.Equal(8, S_EnterRoomPacket.Size);
        }

        [Fact]
        public void SEnterRoomPacket_ResultSuccess_RoomId_Preserved()
        {
            var packet = new S_EnterRoomPacket { ResultCode = S_EnterRoomPacket.ResultSuccess, RoomId = 5 };
            byte[] buf = new byte[S_EnterRoomPacket.Size];
            packet.Serialize(buf);

            var result = new S_EnterRoomPacket();
            result.Deserialize(buf);

            Assert.Equal(S_EnterRoomPacket.ResultSuccess, result.ResultCode);
            Assert.Equal(5, result.RoomId);
        }

        [Fact]
        public void SEnterRoomPacket_ResultRoomNotFound_SerializesCorrectly()
        {
            var packet = new S_EnterRoomPacket { ResultCode = S_EnterRoomPacket.ResultRoomNotFound, RoomId = 0 };
            byte[] buf = new byte[S_EnterRoomPacket.Size];
            packet.Serialize(buf);

            var result = new S_EnterRoomPacket();
            result.Deserialize(buf);

            Assert.Equal(S_EnterRoomPacket.ResultRoomNotFound, result.ResultCode);
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
