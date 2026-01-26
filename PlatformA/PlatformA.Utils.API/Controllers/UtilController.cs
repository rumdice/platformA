using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        //private static readonly ConcurrentDictionary<string, string> _urlDatabase = new();

        private readonly AppDbContext _db;

        // HttpClient는 무겁기 때문에 static으로 재사용하는 것이 (Socket Exhaustion 방지)
        private static readonly HttpClient _httpClient = new HttpClient();

        public UtilController(AppDbContext db)
        {
            _db = db;
            // 💡 팁: 서버 켤 때 DB가 없으면 자동으로 만들어줍니다. (실무에선 Migrations를 쓰지만 지금은 간편하게!)
            _db.Database.EnsureCreated();
        }

        // 1. 내 IP 조회 및 위치 정보 반환
        // GET: /api/myip -> util/myip
        [HttpGet("myip")]
        public async Task<IActionResult> GetMyIp()
        {

            // 1. [실무용] 리버스 프록시(Nginx, AWS ELB, Cloudflare) 뒤에 있을 경우 진짜 IP 가져오기
            string ip = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();

            // 2. 프록시 헤더가 없으면 직접 접속 IP 가져오기
            if (string.IsNullOrEmpty(ip))
            {
                ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            }

            // IPv6 로컬호스트(::1) 처리
            if (ip == "::1") ip = "127.0.0.1";

            // 🔥 3. [개발 편의용] 만약 로컬호스트(127.0.0.1)라면? 
            // 사용자에게 의미 없는 127.0.0.1 대신, 실제 공인 IP를 외부에서 조회해옵니다.
            // (서버가 클라이언트 대신 외부 서비스에 "나 누구요?" 하고 물어보는 방식)
            if (ip == "127.0.0.1")
            {
                try
                {
                    // 외부 무료 API를 잠시 빌려 씁니다. (AWS 체크ip 등 사용 가능)
                    ip = await _httpClient.GetStringAsync("https://api.ipify.org");
                }
                catch
                {
                    // 외부 통신 실패 시 그냥 127.0.0.1 반환
                }
            }

            var response = new
            {
                // TODO: 실제 위치 정보 API 연동 필요
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
        public async Task<IActionResult> ShortenUrlAsync([FromBody] UrlRequestDto request)
        {
            // 유효성 검사
            if (string.IsNullOrWhiteSpace(request.Url) || !Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
            {
                return BadRequest("유효하지 않은 URL입니다."); // 400 Bad Request
            }

            // 랜덤 코드 생성 (6자리)
            var shortCode = Guid.NewGuid().ToString().Substring(0, 6);

            // 메모리에 저장
            //_urlDatabase[shortCode] = request.Url;

            // DB에 저장
            var shortUrlEntry = new Models.DB.ShortUrl
            {
                Code = shortCode,
                OriginalUrl = request.Url
            };

            _db.ShortUrls.Add(shortUrlEntry); // 메모리에 추가하고
            await _db.SaveChangesAsync(); // 진짜 DB(파일)에 저장!

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
        public async Task<IActionResult> RedirectUrlAsync(string code)
        {
            // 서버 메모리에서 찾기
            //if (_urlDatabase.TryGetValue(code, out var originalUrl))
            //{
            //    return Redirect(originalUrl); // 302 Found (리다이렉트)
            //}

            // DB에서 찾기.
            var urlItem = await _db.ShortUrls.FirstOrDefaultAsync(u => u.Code == code);

            if (urlItem != null)
            {
                urlItem.ClickCount++;

                await _db.SaveChangesAsync(); // 클릭 수 업데이트 저장

                return Redirect(urlItem.OriginalUrl);
            }

            return NotFound("존재하지 않는 단축 URL입니다."); // 404 Not Found
        }

        // 4. 통계 조회 API
        // GET: /api/stats/{code}
        [HttpGet("stats/{code}")]
        public async Task<IActionResult> GetStats(string code)
        {
            var urlItem = await _db.ShortUrls.FirstOrDefaultAsync(u => u.Code == code);

            if (urlItem == null) return NotFound("코드를 찾을 수 없습니다.");

            // 필요한 정보만 골라서 줍니다.
            var stats = new
            {
                Code = urlItem.Code,
                OriginalUrl = urlItem.OriginalUrl,
                ClickCount = urlItem.ClickCount,
                CreatedAt = urlItem.CreatedAt
            };

            return Ok(stats);
        }
    }
}
