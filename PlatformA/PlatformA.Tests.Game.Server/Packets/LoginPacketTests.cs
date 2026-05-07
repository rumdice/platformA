using System.Buffers.Binary;
using System.Text;
using PlatformA.Library.Packets;

namespace PlatformA.Tests.Game.Server.Packets
{
    public class LoginPacketTests
    {
        [Fact]
        public void SLoginPacket_Serialize_ProducesCorrectByteLength()
        {
            Assert.Equal(8, S_LoginPacket.Size);
        }

        [Fact]
        public void SLoginPacket_ResultSuccess_ByteLayout_IsCorrect()
        {
            var packet = new S_LoginPacket { ResultCode = S_LoginPacket.ResultSuccess, PlayerId = 0 };
            byte[] buf = new byte[S_LoginPacket.Size];
            packet.Serialize(buf);

            Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(buf.AsSpan(0, 4)));
        }

        [Fact]
        public void SLoginPacket_PlayerId_Preserved_InSerializedBytes()
        {
            var packet = new S_LoginPacket { ResultCode = S_LoginPacket.ResultSuccess, PlayerId = 12345 };
            byte[] buf = new byte[S_LoginPacket.Size];
            packet.Serialize(buf);

            Assert.Equal(12345, BinaryPrimitives.ReadInt32LittleEndian(buf.AsSpan(4, 4)));
        }

        [Theory]
        [InlineData(S_LoginPacket.ResultSuccess)]
        [InlineData(S_LoginPacket.ResultInvalidToken)]
        [InlineData(S_LoginPacket.ResultNotInQueue)]
        [InlineData(S_LoginPacket.ResultDuplicate)]
        [InlineData(S_LoginPacket.ResultRoomNotFound)]
        public void SLoginPacket_AllResultCodes_SerializeCorrectly(int code)
        {
            var packet = new S_LoginPacket { ResultCode = code, PlayerId = 0 };
            byte[] buf = new byte[S_LoginPacket.Size];
            packet.Serialize(buf);

            Assert.Equal(code, BinaryPrimitives.ReadInt32LittleEndian(buf.AsSpan(0, 4)));
        }

        [Fact]
        public void CLoginPacket_Deserialize_ExtractsRoomId()
        {
            const int expectedRoomId = 42;
            byte[] tokenBytes = Encoding.UTF8.GetBytes("test.jwt.token");
            byte[] buf = new byte[4 + 2 + tokenBytes.Length];
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(0, 4), expectedRoomId);
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4, 2), (ushort)tokenBytes.Length);
            tokenBytes.CopyTo(buf, 6);

            var packet = new C_LoginPacket();
            packet.Deserialize(buf);

            Assert.Equal(expectedRoomId, packet.RoomId);
        }

        [Fact]
        public void CLoginPacket_Deserialize_ExtractsJwtToken()
        {
            const string expectedToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";
            byte[] tokenBytes = Encoding.UTF8.GetBytes(expectedToken);
            byte[] buf = new byte[4 + 2 + tokenBytes.Length];
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(0, 4), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4, 2), (ushort)tokenBytes.Length);
            tokenBytes.CopyTo(buf, 6);

            var packet = new C_LoginPacket();
            packet.Deserialize(buf);

            Assert.Equal(expectedToken, packet.JwtToken);
        }

        [Fact]
        public void CLoginPacket_Deserialize_EmptyToken_ReturnsEmpty()
        {
            byte[] buf = new byte[4 + 2];
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(0, 4), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4, 2), 0);

            var packet = new C_LoginPacket();
            packet.Deserialize(buf);

            Assert.Equal(string.Empty, packet.JwtToken);
        }

        [Fact]
        public void CLoginPacket_Deserialize_LongToken_RoundTrip()
        {
            const string longToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IlBsYXllciIsImlhdCI6MTUxNjIzOTAyMn0.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";
            byte[] tokenBytes = Encoding.UTF8.GetBytes(longToken);
            byte[] buf = new byte[4 + 2 + tokenBytes.Length];
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(0, 4), 99);
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4, 2), (ushort)tokenBytes.Length);
            tokenBytes.CopyTo(buf, 6);

            var packet = new C_LoginPacket();
            packet.Deserialize(buf);

            Assert.Equal(longToken, packet.JwtToken);
            Assert.Equal(99, packet.RoomId);
        }
    }
}
