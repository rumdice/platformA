# 요구사항 명세: EnhanceTicketingTests

작성일: 2026-06-30
브랜치: 2026-06-30_EnhanceTicketingTests
소스: /workflow 직접 입력

## 요구사항 요약
Ticketing.API 통합 테스트를 13개에서 21개(이상)로 보강하여 대기열 만료·중복·초과·미존재·SmartPollDelay 등 미커버 경로를 검증한다.

## 상세 요구사항

### 기존 커버리지 (13개)
- **QueueControllerTests.cs** (9개): EnterQueue(valid/noToken), GetStatus(waiting/active/notFound/noToken), LeaveQueue(inQueue/notInQueue/noToken)
- **BearerSecuritySchemeTransformerTests.cs** (4개): OpenAPI 스키마 변환 로직

### 추가할 테스트 (8개)

1. **`EnterQueue_InvalidToken_Returns401`**
   - 조건: `Authorization: Bearer invalid.jwt.token` 헤더
   - 기대: `GetUserIdFromToken` → -1 → 401
   - Mock: 기본값 유지

2. **`EnterQueue_QueueFull_Returns400`**
   - 조건: `ScriptEvaluateAsync` (queue key, `!rl:*`) → `-1L`
   - 기대: `RegisterQueueAsync` returns false → 400 "대기열 큐가 오버했다."
   - Mock: `It.Is<RedisKey[]>(keys => !((string)keys[0]).StartsWith("rl:"))` → `-1L`

3. **`GetStatus_HighRank_Returns10000msDelay`**
   - 조건: `ScriptEvaluateAsync` (non-rl) → `200L` (rank 200, > 100)
   - 기대: `NextPollDelay == 10000`
   - Mock: `200L` 반환

4. **`GetStatus_MidRank_Returns5000msDelay`**
   - 조건: `ScriptEvaluateAsync` (non-rl) → `75L` (rank 75, > 50)
   - 기대: `NextPollDelay == 5000`

5. **`GetStatus_LowRank_Returns3000msDelay`**
   - 조건: `ScriptEvaluateAsync` (non-rl) → `30L` (rank 30, > 10)
   - 기대: `NextPollDelay == 3000`

6. **`LeaveQueue_ActiveTicket_Returns400`**
   - 조건: `ScriptEvaluateAsync` (non-rl) → `0L`, `KeyExistsAsync` → `true`
   - 기대: `LeaveQueueAsync` false → `IsActiveAsync` true → 400 "이미 입장권이 발급된 상태입니다."

7. **`LeaveQueue_InvalidToken_Returns401`**
   - 조건: `Authorization: Bearer invalid.jwt.token`
   - 기대: `GetUserIdFromToken` → -1 → 401

8. **`EnterQueue_RateLimitExceeded_Returns429`**
   - 조건: `ScriptEvaluateAsync` for `rl:queue:*` → `0L`
   - 기대: `[RedisRateLimit("queue")]` 필터 차단 → 429
   - `try/finally`로 기본값 복원 필수

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|------|---------|
| `PlatformA.Tests.Ticketing.API/QueueControllerTests.cs` | 테스트 8개 추가 |

## 제약 및 주의사항
- **기존 팩토리 패턴 재사용** — `TicketingTestWebAppFactory`의 Reflection 주입 패턴
- **Rate Limit 공유 주의** — ScriptEvaluateAsync 오버라이드 시 `rl:*` 키 필터 및 `try/finally` 복원 필수
- **`IClassFixture` 공유 인스턴스** — 각 테스트는 독립적이어야 함, 상태 잔존 불가
- **`tests.md` 규칙 준수** — `{동작}_{조건}_{예상결과}` 네이밍, 영어만 사용

## 구현 접근 방향

```csharp
// 패턴 예시: 큐 오버플로우 (rl:* 제외하고 -1L 반환)
_factory.MockRedisDb
    .Setup(x => x.ScriptEvaluateAsync(
        It.IsAny<string>(),
        It.Is<RedisKey[]>(keys => keys != null && keys.Length > 0
            && !((string)keys[0]).StartsWith("rl:")),
        It.IsAny<RedisValue[]>(),
        It.IsAny<CommandFlags>()))
    .ReturnsAsync(RedisResult.Create(-1L));

try {
    var client = CreateAuthenticatedClient(N);
    var response = await client.PostAsync("/api/queue/enter", null);
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
} finally {
    // 기본값 1L로 복원
    _factory.MockRedisDb
        .Setup(x => x.ScriptEvaluateAsync(...))
        .ReturnsAsync(RedisResult.Create(1L));
}
```

## 검증 기준

1. `dotnet test PlatformA.Tests.Ticketing.API` — 21개 이상 통과 (기존 13 + 신규 8)
2. `dotnet test PlatformA.sln -q` — 전체 216 + 8 = 224개 이상 통과
3. 각 추가 테스트가 독립적으로 실행되고 다른 테스트에 영향 없음
