using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PlatformA.Utils.API.Models;
using System.Collections.Concurrent;

namespace PlatformA.Utils.API.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class UtilController : ControllerBase
    {
        // 🔥 중요: 컨트롤러는 요청마다 새로 생성됩니다 (Transient).
        // 따라서 데이터가 유지되려면 변수를 static으로 선언해야 합니다.
        // (나중에 DB를 연결하면 static을 뺄 것입니다)
        private static readonly ConcurrentDictionary<string, string> _urlDatabase = new();

        // 1. 내 IP 조회 및 위치 정보 반환
        // GET: /api/myip -> util/myip
        [HttpGet("myip")]
        public IActionResult GetMyIp()
        {
            Console.WriteLine("GetMyIp 컨트롤러 호출됨.");

            // 컨트롤러에서는 HttpContext 속성으로 바로 접근 가능합니다.
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            // 로컬 테스트용 IPv6 처리
            if (ip == "::1") ip = "127.0.0.1";

            var response = new
            {
                ip = ip,
                city = "Seoul (Controller Ver.)", // 컨트롤러 작동 확인용 마킹
                region = "KR",
                country_name = "South Korea",
                org = "My Level2 Server",
                latitude = 37.5665,
                longitude = 126.9780
            };

            return Ok(response); // 200 OK + JSON
        }

        // 2. URL 단축 요청
        // POST: /api/shorten
        [HttpPost("shorten")]
        public IActionResult ShortenUrl([FromBody] UrlRequestDto request)
        {
            // 유효성 검사
            if (string.IsNullOrWhiteSpace(request.Url) || !Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
            {
                return BadRequest("유효하지 않은 URL입니다."); // 400 Bad Request
            }

            // 랜덤 코드 생성 (6자리)
            var shortCode = Guid.NewGuid().ToString().Substring(0, 6);

            // 메모리에 저장
            _urlDatabase[shortCode] = request.Url;

            // 결과 반환
            // Request.Scheme = http/https, Request.Host = localhost:5000 등
            var shortUrl = $"{Request.Scheme}://{Request.Host}/go/{shortCode}";

            return Ok(new { shortUrl = shortUrl, code = shortCode });
        }

        // 3. 단축 URL 리다이렉트
        // GET: /go/{code}
        // 컨트롤러 상단에 [Route("api")]가 있어도, 
        // 메서드에 "/"로 시작하는 라우트를 쓰면 절대 경로로 오버라이드 됩니다.
        [HttpGet("/go/{code}")]
        public IActionResult RedirectUrl(string code)
        {
            if (_urlDatabase.TryGetValue(code, out var originalUrl))
            {
                return Redirect(originalUrl); // 302 Found (리다이렉트)
            }

            return NotFound("존재하지 않는 단축 URL입니다."); // 404 Not Found
        }
    }
}
