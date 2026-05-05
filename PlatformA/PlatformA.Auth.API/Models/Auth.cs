using System.ComponentModel.DataAnnotations;

namespace PlatformA.Auth.API.Models
{
    // DTO

    // 클라이언트가 서버로 보내는 로그인 요청 데이터
    public class LoginRequest
    {
        [Required(ErrorMessage = "Username은 필수입니다.")]
        [MinLength(3, ErrorMessage = "Username은 최소 3자 이상이어야 합니다.")]
        [MaxLength(20, ErrorMessage = "Username은 최대 20자 이하여야 합니다.")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username은 영문, 숫자, 밑줄(_)만 허용됩니다.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password는 필수입니다.")]
        [MinLength(6, ErrorMessage = "Password는 최소 6자 이상이어야 합니다.")]
        [MaxLength(100, ErrorMessage = "Password는 최대 100자 이하여야 합니다.")]
        public string Password { get; set; } = string.Empty;
    }

    // 서버가 클라이언트로 돌려주는 로그인 응답 데이터
    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Token { get; set; } = string.Empty;         // Access Token (15분)
        public string RefreshToken { get; set; } = string.Empty;  // Refresh Token (7일, Redis 관리)
        public int PlayerId { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    // Refresh Token으로 새 Access Token 요청
    public class RefreshRequest
    {
        [Required(ErrorMessage = "RefreshToken은 필수입니다.")]
        public string RefreshToken { get; set; } = string.Empty;
    }

    // Refresh / Logout 요청 시 RefreshToken 전달
    public class LogoutRequest
    {
        [Required(ErrorMessage = "RefreshToken은 필수입니다.")]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
