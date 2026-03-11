namespace PlatformA.Auth.API.Models
{
    // DTO

    // 클라이언트가 서버로 보내는 로그인 요청 데이터
    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
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
