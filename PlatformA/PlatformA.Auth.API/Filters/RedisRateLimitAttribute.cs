using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PlatformA.Library.RateLimit;

namespace PlatformA.Auth.API.Filters
{
    /// <summary>
    /// Redis 기반 분산 Rate Limiting 속성.
    /// [EnableRateLimiting] 대신 사용하며, 여러 인스턴스 간 카운터를 공유합니다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RedisRateLimitAttribute : Attribute, IFilterFactory
    {
        public string PolicyName { get; }
        public bool IsReusable => false;

        public RedisRateLimitAttribute(string policyName)
        {
            PolicyName = policyName;
        }

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            var service = serviceProvider.GetRequiredService<RedisRateLimiterService>();
            return new RedisRateLimitFilter(service, PolicyName);
        }
    }

    internal class RedisRateLimitFilter : IAsyncResourceFilter
    {
        private readonly RedisRateLimiterService _service;
        private readonly string _policyName;

        public RedisRateLimitFilter(RedisRateLimiterService service, string policyName)
        {
            _service = service;
            _policyName = policyName;
        }

        public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
        {
            // 리버스 프록시(Nginx, ALB 등) 뒤에서도 실제 클라이언트 IP를 정확히 추출
            string clientIp = context.HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                           ?? context.HttpContext.Connection.RemoteIpAddress?.ToString()
                           ?? "unknown";

            bool allowed = await _service.IsAllowedAsync(_policyName, clientIp);

            if (!allowed)
            {
                context.Result = new ContentResult
                {
                    StatusCode = 429,
                    Content = "Too many requests. Please try again later.",
                    ContentType = "text/plain"
                };
                return;
            }

            await next();
        }
    }
}
