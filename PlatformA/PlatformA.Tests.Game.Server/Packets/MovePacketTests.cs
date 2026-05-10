using Google.Protobuf;
using PlatformA.Library.Packets;

namespace PlatformA.Tests.Game.Server.Packets
{
    public class MovePacketTests
    {
        [Fact]
        public void CMove_RoundTrip_PreservesAllFields()
        {
            var original = new CMove { X = 1.5f, Y = -2.5f, Z = 100.75f };
            var result = CMove.Parser.ParseFrom(original.ToByteArray());

            Assert.Equal(original.X, result.X);
            Assert.Equal(original.Y, result.Y);
            Assert.Equal(original.Z, result.Z);
        }

        [Fact]
        public void CMove_Zero_Coordinates_RoundTrip()
        {
            var original = new CMove { X = 0f, Y = 0f, Z = 0f };
            var result = CMove.Parser.ParseFrom(original.ToByteArray());

            Assert.Equal(0f, result.X);
            Assert.Equal(0f, result.Y);
            Assert.Equal(0f, result.Z);
        }

        [Fact]
        public void CMove_NegativeCoordinates_RoundTrip()
        {
            var original = new CMove { X = -999.9f, Y = -0.001f, Z = float.MinValue };
            var result = CMove.Parser.ParseFrom(original.ToByteArray());

            Assert.Equal(original.X, result.X);
            Assert.Equal(original.Y, result.Y);
            Assert.Equal(original.Z, result.Z);
        }

        [Fact]
        public void SMove_RoundTrip_PreservesAllFields()
        {
            var original = new SMove { PlayerId = 42, X = 1.0f, Y = 2.0f, Z = 3.0f };
            var result = SMove.Parser.ParseFrom(original.ToByteArray());

            Assert.Equal(original.PlayerId, result.PlayerId);
            Assert.Equal(original.X, result.X);
            Assert.Equal(original.Y, result.Y);
            Assert.Equal(original.Z, result.Z);
        }

        [Fact]
        public void SMove_MaxPlayerId_Preserved()
        {
            var original = new SMove { PlayerId = int.MaxValue, X = 0f, Y = 0f, Z = 0f };
            var result = SMove.Parser.ParseFrom(original.ToByteArray());

            Assert.Equal(int.MaxValue, result.PlayerId);
        }
    }
}
