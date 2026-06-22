using Google.Protobuf;
using PlatformA.Library.Packets;

namespace PlatformA.Tests.Game.Lobby.Packets
{
    public class LoginPacketTests
    {
        [Fact]
        public void SLogin_RoundTrip_ResultCode_And_PlayerId()
        {
            var original = new SLogin { ResultCode = LoginResultCode.LoginSuccess, PlayerId = 12345 };
            var result = SLogin.Parser.ParseFrom(original.ToByteArray());

            Assert.Equal(LoginResultCode.LoginSuccess, result.ResultCode);
            Assert.Equal(12345, result.PlayerId);
        }

        [Theory]
        [InlineData(LoginResultCode.LoginSuccess)]
        [InlineData(LoginResultCode.LoginInvalidToken)]
        [InlineData(LoginResultCode.LoginNotInQueue)]
        [InlineData(LoginResultCode.LoginDuplicate)]
        [InlineData(LoginResultCode.LoginRoomNotFound)]
        public void SLogin_AllResultCodes_RoundTrip(LoginResultCode code)
        {
            var msg = new SLogin { ResultCode = code, PlayerId = 0 };
            var result = SLogin.Parser.ParseFrom(msg.ToByteArray());

            Assert.Equal(code, result.ResultCode);
        }

        [Fact]
        public void CLogin_RoundTrip_ExtractsRoomId()
        {
            var original = new CLogin { RoomId = 42, JwtToken = "test.jwt.token" };
            var result = CLogin.Parser.ParseFrom(original.ToByteArray());

            Assert.Equal(42, result.RoomId);
        }

        [Fact]
        public void CLogin_RoundTrip_ExtractsJwtToken()
        {
            const string token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";
            var original = new CLogin { RoomId = 1, JwtToken = token };
            var result = CLogin.Parser.ParseFrom(original.ToByteArray());

            Assert.Equal(token, result.JwtToken);
        }

        [Fact]
        public void CLogin_EmptyToken_RoundTrip()
        {
            var original = new CLogin { RoomId = 1, JwtToken = "" };
            var result = CLogin.Parser.ParseFrom(original.ToByteArray());

            Assert.Equal("", result.JwtToken);
        }

        [Fact]
        public void CLogin_LongToken_RoundTrip()
        {
            const string longToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IlBsYXllciIsImlhdCI6MTUxNjIzOTAyMn0.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";
            var original = new CLogin { RoomId = 99, JwtToken = longToken };
            var result = CLogin.Parser.ParseFrom(original.ToByteArray());

            Assert.Equal(longToken, result.JwtToken);
            Assert.Equal(99, result.RoomId);
        }
    }
}
