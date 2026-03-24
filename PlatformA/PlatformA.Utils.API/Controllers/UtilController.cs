using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlatformA.Library.Helper;
using PlatformA.Utils.API.Models;
using StackExchange.Redis;

namespace PlatformA.Utils.API.Controllers
{
    /// <summary>
    /// atomicUtils 유틸리티 페이지에서 요청하는 벡엔드 API
    /// </summary>
    [Route("[controller]")]
    [ApiController]
    public class UtilController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly SnowflakeGenerator _snowflake;
        private readonly IDatabase _redis; // ex) Redis의 0번 DB를 가리킴.

        // 🔥 중요: 컨트롤러는 요청마다 새로 생성됩니다 (Transient).
        // 따라서 데이터가 유지되려면 변수를 static으로 선언해야 합니다.
        // HttpClient는 무겁기 때문에 static으로 재사용하는 것이 (Socket Exhaustion 방지)
        private static readonly HttpClient _httpClient = new HttpClient();

        public UtilController(AppDbContext db, SnowflakeGenerator snowflake, IConnectionMultiplexer redisMux)
        {
            // 💡 팁: 서버 켤 때 DB가 없으면 자동으로 만들어줍니다. (실무에선 Migrations를 쓰지만 지금은 간편하게!)
            _db = db;
            _db.Database.EnsureCreated();
            
            _redis = redisMux.GetDatabase();
            _snowflake = snowflake;
        }

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
            if (ip == "::1") 
                ip = "127.0.0.1";

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

        [HttpPost("shorten")]
        public async Task<IActionResult> ShortenUrlAsync([FromBody] UrlRequestDto request)
        {
            // 유효성 검사
            if (string.IsNullOrWhiteSpace(request.Url) || !Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
            {
                return BadRequest("유효하지 않은 URL입니다."); // 400 Bad Request
            }

            // 랜덤 코드 생성 (6자리)
            //var shortCode = Guid.NewGuid().ToString().Substring(0, 6);
            
            // 중복 위험성이 높고 DB B-tree 인덱스 성능이 떨어지므로, Guid 를 Snowflake로 교체
            long newId = _snowflake.NextId();

            // 숫자를 Base62 문자로 변환 (예: 12345678 -> "Tx9z")
            string shortCode = Base62Converter.Encode(newId);

            // DB에 저장
            var shortUrlEntry = new Models.DB.ShortUrl
            {
                Id = newId,
                Code = shortCode,
                OriginalUrl = request.Url
            };

            _db.ShortUrls.Add(shortUrlEntry); // 메모리에 추가하고
            await _db.SaveChangesAsync(); // 진짜 DB(파일)에 저장!

            // 결과 반환
            var shortUrl = $"{Request.Scheme}://{Request.Host}/go/{shortCode}";
            return Ok(new { shortUrl = shortUrl, code = shortCode });
        }

        [HttpGet("/go/{code}")]
        public async Task<IActionResult> RedirectUrlAsync(string code)
        {
            // 서버 메모리에서 찾기
            //if (_urlDatabase.TryGetValue(code, out var originalUrl))
            //{
            //    return Redirect(originalUrl); // 302 Found (리다이렉트)
            //}

            string cacheKey = $"url:{code}"; // Redis 키 규칙 (예: url:Tx9z) 1. URL 정보 (원본 주소)
            string statsKey = $"stats:{code}";  // 2. 조회수 정보 (숫자)

            string originalUrl = null;

            // Redis 에서 찾기.
            var cachedUrl = await _redis.StringGetAsync(cacheKey);

            
            if (!cachedUrl.IsNullOrEmpty) // 캐시 히트
            {
                originalUrl = cachedUrl.ToString();
            }
            else // 캐시 미스
            {
                // DB에서 찾기.
                var urlItem = await _db.ShortUrls.FirstOrDefaultAsync(u => u.Code == code);
                if (urlItem == null)
                {
                    return NotFound("존재하지 않는 단축 URL입니다."); // 404 Not Found
                }

                originalUrl = urlItem.OriginalUrl;

                // Redis에 URL 정보 저장 (TTL : 10분)
                await _redis.StringSetAsync(cacheKey, originalUrl, TimeSpan.FromMinutes(10));

                //  URL이 만료되었다는 건 stats도 만료되었을 확률이 높으므로 초기화가 안전합니다.)
                // 현재 조회수도 Redis에 세팅 (DB에서 가져온 조회수로 초기화)
                await _redis.StringSetAsync(statsKey, urlItem.ClickCount);
            }

            // ---------------------------------------------------------
            // 🔥 C. [Write-Back] 여기가 핵심 변경 사항입니다!
            // ---------------------------------------------------------

            // 1. DB에 바로 쓰지 않고, Redis 메모리에서 숫자만 1 올립니다. (Atomic)
            // INCR 명령어는 키가 없으면 0->1로 만들고, 있으면 +1 합니다.
            await _redis.StringIncrementAsync(statsKey);

            // 2. "이 코드(Tx9z)는 변경되었으니 나중에 DB에 저장해야 해"라고 명단(Set)에 적습니다.
            // dirty_codes라는 Set에 중복 없이 담깁니다.
            await _redis.SetAddAsync("dirty_codes", code);

            // 3. 일단 리다이렉트
            return Redirect(originalUrl);
            
            // 4. 일단 리다이렉트로 응답은 던지고 나중에 백그라운드 서비스 StatSyncsService 에서 주기적으로 Redis에서 "dirty_codes" Set을 확인해서 DB에 반영합니다.
        }

        /// <summary>
        /// 클릭수 조회
        /// </summary>
        [HttpGet("stats/{code}")]
        public async Task<IActionResult> GetStats(string code)
        {
            var urlItem = await _db.ShortUrls.AsNoTracking().FirstOrDefaultAsync(u => u.Code == code);
            if (urlItem == null) 
                return NotFound("코드를 찾을 수 없습니다.");

            // Redis에 최신 카운트가 있는지 확인
            var redisCount = await _redis.StringGetAsync($"stats:{code}");
            int finalCount = urlItem.ClickCount; // 기본은 DB 값

            if (redisCount.HasValue && int.TryParse(redisCount, out int rCount))
            {
                finalCount = rCount; // Redis 값이 있으면 그게 '진짜' 최신 값, 없으면 DB 값 그대로 사용
            }

            // 필요한 정보만 골라서 줍니다.
            var stats = new
            {
                Code = urlItem.Code,
                OriginalUrl = urlItem.OriginalUrl,
                ClickCount = finalCount,
                CreatedAt = urlItem.CreatedAt
            };

            return Ok(stats);
        }
    }
}
