using System.Buffers.Binary;
using Google.Protobuf;
using PlatformA.Library.Packets;

namespace PlatformA.Tests.Game.Server.Packets
{
    public class PacketFramingTests
    {
        private static byte[] Frame(PacketID id, IMessage message)
        {
            byte[] payload = message.ToByteArray();
            ushort size = (ushort)(4 + payload.Length);
            byte[] buf = new byte[size];
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), size);
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2, 2), (ushort)id);
            payload.CopyTo(buf, 4);
            return buf;
        }

        // ── Header encoding ────────────────────────────────────────────

        [Theory]
        [InlineData(PacketID.S_Move)]
        [InlineData(PacketID.S_Login)]
        [InlineData(PacketID.S_EnterRoom)]
        public void Frame_ServerResponse_SizeBytesEqualTotalLength(PacketID id)
        {
            byte[] frame = Frame(id, new SMove { PlayerId = 1, X = 0f, Y = 0f, Z = 0f });
            ushort encoded = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(0, 2));
            Assert.Equal((ushort)frame.Length, encoded);
        }

        [Theory]
        [InlineData(PacketID.S_Move, (ushort)PacketID.S_Move)]
        [InlineData(PacketID.S_Login, (ushort)PacketID.S_Login)]
        [InlineData(PacketID.S_EnterRoom, (ushort)PacketID.S_EnterRoom)]
        public void Frame_ServerResponse_PacketIdBytesMatchEnum(PacketID id, ushort expectedId)
        {
            byte[] frame = Frame(id, new SLogin { ResultCode = LoginResultCode.LoginSuccess, PlayerId = 0 });
            ushort encoded = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2, 2));
            Assert.Equal(expectedId, encoded);
        }

        [Fact]
        public void Frame_AnyMessage_MinimumLengthIsFourBytes()
        {
            byte[] frame = Frame(PacketID.S_Login, new SLogin());
            Assert.True(frame.Length >= 4);
        }

        // ── SLogin framing ────────────────────────────────────────────

        [Theory]
        [InlineData(LoginResultCode.LoginSuccess, 42)]
        [InlineData(LoginResultCode.LoginInvalidToken, 0)]
        [InlineData(LoginResultCode.LoginNotInQueue, 0)]
        [InlineData(LoginResultCode.LoginDuplicate, 0)]
        [InlineData(LoginResultCode.LoginRoomNotFound, 0)]
        public void Frame_SLogin_PayloadParsesAllResultCodes(LoginResultCode code, int playerId)
        {
            byte[] frame = Frame(PacketID.S_Login, new SLogin { ResultCode = code, PlayerId = playerId });
            var parsed = SLogin.Parser.ParseFrom(frame, 4, frame.Length - 4);

            Assert.Equal(code, parsed.ResultCode);
            Assert.Equal(playerId, parsed.PlayerId);
        }

        // ── SMove framing ─────────────────────────────────────────────

        [Fact]
        public void Frame_SMove_PayloadPreservesAllFields()
        {
            var original = new SMove { PlayerId = 99, X = 1.5f, Y = -2.5f, Z = 100.0f };
            byte[] frame = Frame(PacketID.S_Move, original);
            var parsed = SMove.Parser.ParseFrom(frame, 4, frame.Length - 4);

            Assert.Equal(original.PlayerId, parsed.PlayerId);
            Assert.Equal(original.X, parsed.X);
            Assert.Equal(original.Y, parsed.Y);
            Assert.Equal(original.Z, parsed.Z);
        }

        [Fact]
        public void Frame_SMove_PacketIdIsCorrect()
        {
            byte[] frame = Frame(PacketID.S_Move, new SMove { PlayerId = 1 });
            ushort packetId = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2, 2));
            Assert.Equal((ushort)PacketID.S_Move, packetId);
        }

        // ── SEnterRoom framing ────────────────────────────────────────

        [Theory]
        [InlineData(EnterRoomResultCode.EnterRoomSuccess, 3)]
        [InlineData(EnterRoomResultCode.EnterRoomNotFound, 0)]
        public void Frame_SEnterRoom_PayloadParsesResultAndRoomId(EnterRoomResultCode code, int roomId)
        {
            byte[] frame = Frame(PacketID.S_EnterRoom, new SEnterRoom { ResultCode = code, RoomId = roomId });
            var parsed = SEnterRoom.Parser.ParseFrom(frame, 4, frame.Length - 4);

            Assert.Equal(code, parsed.ResultCode);
            Assert.Equal(roomId, parsed.RoomId);
        }

        // ── Client request parsing (server receive side) ──────────────

        [Fact]
        public void ParseFrom_CLogin_ExtractsRoomIdAndToken()
        {
            byte[] frame = Frame(PacketID.C_Login, new CLogin { RoomId = 5, JwtToken = "test.token" });
            var parsed = CLogin.Parser.ParseFrom(frame, 4, frame.Length - 4);

            Assert.Equal(5, parsed.RoomId);
            Assert.Equal("test.token", parsed.JwtToken);
        }

        [Fact]
        public void ParseFrom_CMove_ExtractsCoordinates()
        {
            byte[] frame = Frame(PacketID.C_Move, new CMove { X = 3.14f, Y = -1.0f, Z = 0.5f });
            var parsed = CMove.Parser.ParseFrom(frame, 4, frame.Length - 4);

            Assert.Equal(3.14f, parsed.X);
            Assert.Equal(-1.0f, parsed.Y);
            Assert.Equal(0.5f, parsed.Z);
        }

        [Fact]
        public void ParseFrom_CEnterRoom_ExtractsRoomId()
        {
            byte[] frame = Frame(PacketID.C_EnterRoom, new CEnterRoom { RoomId = 7 });
            var parsed = CEnterRoom.Parser.ParseFrom(frame, 4, frame.Length - 4);

            Assert.Equal(7, parsed.RoomId);
        }

        [Fact]
        public void ParseFrom_InvalidBytes_ThrowsInvalidProtocolBufferException()
        {
            byte[] garbage = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC, 0x00 };
            Assert.Throws<Google.Protobuf.InvalidProtocolBufferException>(
                () => SLogin.Parser.ParseFrom(garbage, 4, garbage.Length - 4));
        }
    }
}
