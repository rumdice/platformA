using System.Buffers.Binary;
using Google.Protobuf;
using PlatformA.Library.Packets;

namespace PlatformA.Tests.Game.Server.Packets
{
    public class PacketFramingTests
    {
        // Builds [ushort size: 2B LE][Packet envelope]
        private static byte[] Frame(Packet envelope)
        {
            byte[] envelopeBytes = envelope.ToByteArray();
            ushort size = (ushort)(2 + envelopeBytes.Length);
            byte[] buf = new byte[size];
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), size);
            envelopeBytes.CopyTo(buf, 2);
            return buf;
        }

        private static Packet ParseEnvelope(byte[] frame)
            => Packet.Parser.ParseFrom(frame, 2, frame.Length - 2);

        // ── Header encoding ────────────────────────────────────────────

        [Theory]
        [InlineData(1)]   // SMove
        [InlineData(42)]  // PlayerId boundary
        public void Frame_SMove_SizeBytesEqualTotalLength(int playerId)
        {
            byte[] frame = Frame(new Packet { SMove = new SMove { PlayerId = playerId } });
            ushort encoded = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(0, 2));
            Assert.Equal((ushort)frame.Length, encoded);
        }

        [Fact]
        public void Frame_SLogin_SizeBytesEqualTotalLength()
        {
            byte[] frame = Frame(new Packet { SLogin = new SLogin { ResultCode = LoginResultCode.LoginSuccess, PlayerId = 1 } });
            ushort encoded = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(0, 2));
            Assert.Equal((ushort)frame.Length, encoded);
        }

        [Fact]
        public void Frame_SEnterRoom_SizeBytesEqualTotalLength()
        {
            byte[] frame = Frame(new Packet { SEnterRoom = new SEnterRoom { ResultCode = EnterRoomResultCode.EnterRoomSuccess, RoomId = 1 } });
            ushort encoded = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(0, 2));
            Assert.Equal((ushort)frame.Length, encoded);
        }

        [Fact]
        public void Frame_AnyMessage_MinimumLengthIsTwoBytes()
        {
            byte[] frame = Frame(new Packet { SLogin = new SLogin() });
            Assert.True(frame.Length >= 2);
        }

        // ── PayloadCase discrimination ────────────────────────────────

        [Fact]
        public void ParseEnvelope_SLogin_PayloadCaseIsSLogin()
        {
            byte[] frame = Frame(new Packet { SLogin = new SLogin { ResultCode = LoginResultCode.LoginSuccess, PlayerId = 5 } });
            Packet envelope = ParseEnvelope(frame);
            Assert.Equal(Packet.PayloadOneofCase.SLogin, envelope.PayloadCase);
        }

        [Fact]
        public void ParseEnvelope_SMove_PayloadCaseIsSMove()
        {
            byte[] frame = Frame(new Packet { SMove = new SMove { PlayerId = 1, X = 1f } });
            Packet envelope = ParseEnvelope(frame);
            Assert.Equal(Packet.PayloadOneofCase.SMove, envelope.PayloadCase);
        }

        [Fact]
        public void ParseEnvelope_SEnterRoom_PayloadCaseIsSEnterRoom()
        {
            byte[] frame = Frame(new Packet { SEnterRoom = new SEnterRoom { ResultCode = EnterRoomResultCode.EnterRoomSuccess, RoomId = 2 } });
            Packet envelope = ParseEnvelope(frame);
            Assert.Equal(Packet.PayloadOneofCase.SEnterRoom, envelope.PayloadCase);
        }

        [Fact]
        public void ParseEnvelope_CLogin_PayloadCaseIsCLogin()
        {
            byte[] frame = Frame(new Packet { CLogin = new CLogin { RoomId = 1, JwtToken = "tok" } });
            Packet envelope = ParseEnvelope(frame);
            Assert.Equal(Packet.PayloadOneofCase.CLogin, envelope.PayloadCase);
        }

        [Fact]
        public void ParseEnvelope_CMove_PayloadCaseIsCMove()
        {
            byte[] frame = Frame(new Packet { CMove = new CMove { X = 1f, Y = 2f, Z = 3f } });
            Packet envelope = ParseEnvelope(frame);
            Assert.Equal(Packet.PayloadOneofCase.CMove, envelope.PayloadCase);
        }

        [Fact]
        public void ParseEnvelope_CEnterRoom_PayloadCaseIsCEnterRoom()
        {
            byte[] frame = Frame(new Packet { CEnterRoom = new CEnterRoom { RoomId = 7 } });
            Packet envelope = ParseEnvelope(frame);
            Assert.Equal(Packet.PayloadOneofCase.CEnterRoom, envelope.PayloadCase);
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
            byte[] frame = Frame(new Packet { SLogin = new SLogin { ResultCode = code, PlayerId = playerId } });
            Packet envelope = ParseEnvelope(frame);

            Assert.Equal(Packet.PayloadOneofCase.SLogin, envelope.PayloadCase);
            Assert.Equal(code, envelope.SLogin.ResultCode);
            Assert.Equal(playerId, envelope.SLogin.PlayerId);
        }

        // ── SMove framing ─────────────────────────────────────────────

        [Fact]
        public void Frame_SMove_PayloadPreservesAllFields()
        {
            var original = new SMove { PlayerId = 99, X = 1.5f, Y = -2.5f, Z = 100.0f };
            byte[] frame = Frame(new Packet { SMove = original });
            Packet envelope = ParseEnvelope(frame);

            Assert.Equal(Packet.PayloadOneofCase.SMove, envelope.PayloadCase);
            Assert.Equal(original.PlayerId, envelope.SMove.PlayerId);
            Assert.Equal(original.X, envelope.SMove.X);
            Assert.Equal(original.Y, envelope.SMove.Y);
            Assert.Equal(original.Z, envelope.SMove.Z);
        }

        // ── SEnterRoom framing ────────────────────────────────────────

        [Theory]
        [InlineData(EnterRoomResultCode.EnterRoomSuccess, 3)]
        [InlineData(EnterRoomResultCode.EnterRoomNotFound, 0)]
        public void Frame_SEnterRoom_PayloadParsesResultAndRoomId(EnterRoomResultCode code, int roomId)
        {
            byte[] frame = Frame(new Packet { SEnterRoom = new SEnterRoom { ResultCode = code, RoomId = roomId } });
            Packet envelope = ParseEnvelope(frame);

            Assert.Equal(Packet.PayloadOneofCase.SEnterRoom, envelope.PayloadCase);
            Assert.Equal(code, envelope.SEnterRoom.ResultCode);
            Assert.Equal(roomId, envelope.SEnterRoom.RoomId);
        }

        // ── Client request parsing (server receive side) ──────────────

        [Fact]
        public void ParseEnvelope_CLogin_ExtractsRoomIdAndToken()
        {
            byte[] frame = Frame(new Packet { CLogin = new CLogin { RoomId = 5, JwtToken = "test.token" } });
            Packet envelope = ParseEnvelope(frame);

            Assert.Equal(5, envelope.CLogin.RoomId);
            Assert.Equal("test.token", envelope.CLogin.JwtToken);
        }

        [Fact]
        public void ParseEnvelope_CMove_ExtractsCoordinates()
        {
            byte[] frame = Frame(new Packet { CMove = new CMove { X = 3.14f, Y = -1.0f, Z = 0.5f } });
            Packet envelope = ParseEnvelope(frame);

            Assert.Equal(3.14f, envelope.CMove.X);
            Assert.Equal(-1.0f, envelope.CMove.Y);
            Assert.Equal(0.5f, envelope.CMove.Z);
        }

        [Fact]
        public void ParseEnvelope_CEnterRoom_ExtractsRoomId()
        {
            byte[] frame = Frame(new Packet { CEnterRoom = new CEnterRoom { RoomId = 7 } });
            Packet envelope = ParseEnvelope(frame);

            Assert.Equal(7, envelope.CEnterRoom.RoomId);
        }

        [Fact]
        public void ParseEnvelope_InvalidBytes_ThrowsInvalidProtocolBufferException()
        {
            byte[] garbage = new byte[] { 0x05, 0x00, 0xFF, 0xFE, 0xFD };
            Assert.Throws<Google.Protobuf.InvalidProtocolBufferException>(
                () => Packet.Parser.ParseFrom(garbage, 2, garbage.Length - 2));
        }
    }
}
