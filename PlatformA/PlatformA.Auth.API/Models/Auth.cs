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
        public string Token { get; set; }
        public int PlayerId { get; set; }
        public string Message { get; set; }
    }
}
