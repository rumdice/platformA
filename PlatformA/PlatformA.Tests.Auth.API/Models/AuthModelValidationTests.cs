using System.ComponentModel.DataAnnotations;
using PlatformA.Auth.API.Models;
using Xunit;

namespace PlatformA.Tests.Auth.API.Models
{
    public class AuthModelValidationTests
    {
        private static IList<ValidationResult> Validate(object model)
        {
            var results = new List<ValidationResult>();
            var ctx = new ValidationContext(model);
            Validator.TryValidateObject(model, ctx, results, validateAllProperties: true);
            return results;
        }

        // ── LoginRequest ─────────────────────────────────────────────────────

        [Fact]
        public void LoginRequest_ValidData_PassesValidation()
        {
            var model = new LoginRequest { Username = "valid_user", Password = "pass1234" };
            Assert.Empty(Validate(model));
        }

        [Fact]
        public void LoginRequest_ShortUsername_FailsValidation()
        {
            var model = new LoginRequest { Username = "ab", Password = "pass1234" };
            Assert.Contains(Validate(model), r => r.MemberNames.Contains(nameof(LoginRequest.Username)));
        }

        [Fact]
        public void LoginRequest_LongUsername_FailsValidation()
        {
            var model = new LoginRequest { Username = new string('a', 21), Password = "pass1234" };
            Assert.Contains(Validate(model), r => r.MemberNames.Contains(nameof(LoginRequest.Username)));
        }

        [Fact]
        public void LoginRequest_InvalidChars_FailsValidation()
        {
            // 공백 포함 → ^[a-zA-Z0-9_]+$ 위반
            var model = new LoginRequest { Username = "invalid user", Password = "pass1234" };
            Assert.Contains(Validate(model), r => r.MemberNames.Contains(nameof(LoginRequest.Username)));
        }

        [Fact]
        public void LoginRequest_EmptyUsername_FailsValidation()
        {
            var model = new LoginRequest { Username = "", Password = "pass1234" };
            Assert.Contains(Validate(model), r => r.MemberNames.Contains(nameof(LoginRequest.Username)));
        }

        [Fact]
        public void LoginRequest_ShortPassword_FailsValidation()
        {
            var model = new LoginRequest { Username = "validuser", Password = "pass" };
            Assert.Contains(Validate(model), r => r.MemberNames.Contains(nameof(LoginRequest.Password)));
        }

        [Fact]
        public void LoginRequest_EmptyPassword_FailsValidation()
        {
            var model = new LoginRequest { Username = "validuser", Password = "" };
            Assert.Contains(Validate(model), r => r.MemberNames.Contains(nameof(LoginRequest.Password)));
        }

        // ── RefreshRequest ───────────────────────────────────────────────────

        [Fact]
        public void RefreshRequest_EmptyToken_FailsValidation()
        {
            var model = new RefreshRequest { RefreshToken = "" };
            Assert.Contains(Validate(model), r => r.MemberNames.Contains(nameof(RefreshRequest.RefreshToken)));
        }

        [Fact]
        public void RefreshRequest_ValidToken_PassesValidation()
        {
            var model = new RefreshRequest { RefreshToken = "1:some_valid_looking_token" };
            Assert.Empty(Validate(model));
        }

        // ── LogoutRequest ────────────────────────────────────────────────────

        [Fact]
        public void LogoutRequest_EmptyToken_FailsValidation()
        {
            var model = new LogoutRequest { RefreshToken = "" };
            Assert.Contains(Validate(model), r => r.MemberNames.Contains(nameof(LogoutRequest.RefreshToken)));
        }

        [Fact]
        public void LogoutRequest_ValidToken_PassesValidation()
        {
            var model = new LogoutRequest { RefreshToken = "1:some_valid_looking_token" };
            Assert.Empty(Validate(model));
        }
    }
}
