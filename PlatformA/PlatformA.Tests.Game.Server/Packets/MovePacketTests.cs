using PlatformA.Library.Packets;

namespace PlatformA.Tests.Game.Server.Packets
{
    public class MovePacketTests
    {
        [Fact]
        public void CMovePacket_Serialize_Deserialize_RoundTrip()
        {
            var original = new C_MovePacket { X = 1.5f, Y = -2.5f, Z = 100.75f };
            byte[] buf = new byte[C_MovePacket.Size];
            original.Serialize(buf);

            var result = new C_MovePacket();
            result.Deserialize(buf);

            Assert.Equal(original.X, result.X);
            Assert.Equal(original.Y, result.Y);
            Assert.Equal(original.Z, result.Z);
        }

        [Fact]
        public void CMovePacket_Zero_Coordinates_RoundTrip()
        {
            var original = new C_MovePacket { X = 0f, Y = 0f, Z = 0f };
            byte[] buf = new byte[C_MovePacket.Size];
            original.Serialize(buf);

            var result = new C_MovePacket();
            result.Deserialize(buf);

            Assert.Equal(0f, result.X);
            Assert.Equal(0f, result.Y);
            Assert.Equal(0f, result.Z);
        }

        [Fact]
        public void CMovePacket_Serialize_ProducesCorrectByteLength()
        {
            Assert.Equal(12, C_MovePacket.Size);
        }

        [Fact]
        public void CMovePacket_NegativeCoordinates_RoundTrip()
        {
            var original = new C_MovePacket { X = -999.9f, Y = -0.001f, Z = float.MinValue };
            byte[] buf = new byte[C_MovePacket.Size];
            original.Serialize(buf);

            var result = new C_MovePacket();
            result.Deserialize(buf);

            Assert.Equal(original.X, result.X);
            Assert.Equal(original.Y, result.Y);
            Assert.Equal(original.Z, result.Z);
        }

        [Fact]
        public void SMovePacket_Serialize_Deserialize_RoundTrip()
        {
            var original = new S_MovePacket { PlayerId = 42, X = 1.0f, Y = 2.0f, Z = 3.0f };
            byte[] buf = new byte[S_MovePacket.Size];
            original.Serialize(buf);

            var result = new S_MovePacket();
            result.Deserialize(buf);

            Assert.Equal(original.PlayerId, result.PlayerId);
            Assert.Equal(original.X, result.X);
            Assert.Equal(original.Y, result.Y);
            Assert.Equal(original.Z, result.Z);
        }

        [Fact]
        public void SMovePacket_Serialize_ProducesCorrectByteLength()
        {
            Assert.Equal(16, S_MovePacket.Size);
        }

        [Fact]
        public void SMovePacket_PlayerId_Preserved_AfterRoundTrip()
        {
            var original = new S_MovePacket { PlayerId = int.MaxValue, X = 0f, Y = 0f, Z = 0f };
            byte[] buf = new byte[S_MovePacket.Size];
            original.Serialize(buf);

            var result = new S_MovePacket();
            result.Deserialize(buf);

            Assert.Equal(int.MaxValue, result.PlayerId);
        }
    }
}
