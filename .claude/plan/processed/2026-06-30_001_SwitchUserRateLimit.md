# 요구사항 명세: SwitchUserRateLimit

작성일: 2026-06-30
브랜치: 2026-06-30_SwitchUserRateLimit
소스: /plan 직접 입력 + sprint-077.md

## 요구사항 요약
Auth.API 로그인 Rate Limit 식별자를 IP 기반(`rl:login:{clientIp}`)에서 username 기반(`rl:login:{username}`)으로 전환한다.
E2E 1000명이 동일 IP(::1)를 공유해도 각 유저가 개별 제한을 받아 전원 통과 가능하도록 개선한다.

## 상세 요구사항

1. **`RedisRateLimiterService.IsAllowedAsync` 파라미터명 변경**
   - `string clientIp` → `string identifier` (의미 명확화, 기능 변경 없음)
   - Redis 키 패턴 유지: `rl:{policyName}:{identifier}`

2. **`AuthController.Login` Rate Limit 방식 변경**
   - `[RedisRateLimit("login")]` 어트리뷰트 제거 (IP 기반 제거)
   - 컨트롤러에 `RedisRateLimiterService` 생성자 DI 추가
   - Login 액션 내부에서 `await _rateLimiterService.IsAllowedAsync("login", request.Username)` 호출
   - 차단 시 `StatusCode(429, "Too many requests")` 반환

3. **Consts.cs 상수 추가**
   - `public const string RATE_LIMIT_LOGIN_PREFIX = "rl:login:"` — 모니터링/디버깅용 참조 상수 (실제 키 구성은 서비스 내부에서 처리)

4. **테스트 갱신**
   - `AuthTestWebAppFactory` 내 Rate Limit Mock 키 패턴 확인
   - username 기반 키(`rl:login:{username}`)로 Rate Limit 검증 테스트 추가

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|------|---------|
| `PlatformA.Library/RateLimit/RedisRateLimiterService.cs` | 파라미터명 변경 (`clientIp` → `identifier`) |
| `PlatformA.Library/Common/Consts.cs` | 상수 추가 |
| `PlatformA.Auth.API/Controllers/AuthController.cs` | DI 추가, 어트리뷰트 제거, 내부 호출 추가 |
| `PlatformA.Tests.Auth.API/AuthControllerTests.cs` | username 기반 Rate Limit 테스트 추가 |

## 제약 및 주의사항

- **ADR-001 준수**: Redis Cluster 기반 분산 Rate Limit 유지 — 구현 방식 변경 없음
- **Ticketing.API `[RedisRateLimit]` 어트리뷰트는 변경하지 않는다** — Ticketing은 인증 후 요청이므로 JWT userId 기반으로 별도 처리 가능하지만 이번 스프린트 범위 외
- **`clientIp` 기반 다른 정책** (`register` 등)은 이번 스프린트에서 변경하지 않는다 (로그인만 대상)
- `RedisRateLimiterService`는 Singleton 등록 — `AuthController`(Scoped)에 주입 가능

## 구현 접근 방향

```csharp
// 1. RedisRateLimiterService: 파라미터명 변경 (기능 동일)
public async Task<bool> IsAllowedAsync(string policyName, string identifier)

// 2. AuthController: 어트리뷰트 제거 + 직접 호출
public AuthController(
    ...,
    RedisRateLimiterService rateLimiterService)
{
    _rateLimiterService = rateLimiterService;
}

[HttpPost("login")]  // [RedisRateLimit("login")] 제거
public async Task<IActionResult> Login([FromBody] LoginRequest request)
{
    bool allowed = await _rateLimiterService.IsAllowedAsync("login", request.Username);
    if (!allowed)
        return StatusCode(429, "Too many requests. Please try again later.");
    ...
}
```

## 검증 기준

1. `dotnet build PlatformA.sln` 오류 0개
2. `dotnet test PlatformA.sln` 전체 통과 (기존 23개 + 신규 테스트)
3. Rate Limit 키가 `rl:login:{username}` 형태로 Redis에 저장되는지 테스트에서 검증
4. 동일 IP에서 서로 다른 username으로 요청 시 각자 독립적인 카운터 적용 확인
