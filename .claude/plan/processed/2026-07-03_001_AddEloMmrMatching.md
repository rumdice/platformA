# 요구사항 명세: AddEloMmrMatching

작성일: 2026-07-03
브랜치: 2026-07-03_AddEloMmrMatching
소스: plan mode (~/.claude/plans/7-1-flickering-cook.md) + 사용자 승인 설계

## 요구사항 요약

현재 FIFO(timestamp score) 기반인 `TryMatchAsync`를 ELO 레이팅 기반 3단계 범위 매칭으로
전환한다. 대기 시간 추적에 TTL wait key를 사용하고, K-factor 감소로 MMR 희석을 방지한다.
ELO 계산을 awaitable로 전환하여 통합 테스트 5개를 추가한다.

## 상세 요구사항

### 1. Consts.cs 상수 추가
- `MATCH_RATING_RANGE = 200` — Stage 1 기본 범위
- `MATCH_RATING_RANGE_MID = 400` — Stage 2 (30s 경과)
- `MATCH_RATING_RANGE_WIDE = 800` — Stage 3 (60s 경과)
- `MATCH_WAIT_KEY_PREFIX = "queue:wait:"` — TTL 추적 키 prefix

### 2. GameMatchService.cs 변경

#### 2-1. Lua 스크립트 교체
- `MATCH_STRICT_SCRIPT`: ZRANGEBYSCORE 범위 내 원자적 매칭 (자신 제외)
- `MATCH_FALLBACK_SCRIPT`: ZPOPMIN으로 fallback (자신이면 ZADD 복원)

#### 2-2. TryMatchAsync 3단계 범위 매칭
- score = `GetPlayerRatingAsync(userId)` (ELO 레이팅)
- Stage 1 (즉시): ±200 엄격 범위 → MATCH_STRICT_SCRIPT
- 매칭 실패 시 큐 전체 순회 → 후보의 wait key TTL로 elapsed 계산
  - elapsed 30~60s: ±400 범위 시도
  - elapsed 60~90s: ±800 범위 시도
  - elapsed 90s+: MATCH_FALLBACK_SCRIPT (ZPOPMIN)
- 큐 진입 시 SETEX wait key (TTL=MATCH_TIMEOUT_SECONDS=120, When.NotExists)
- ZADD score = ELO 레이팅 (기존 timestamp 대체)

#### 2-3. CancelMatchAsync
- ZREM + DEL wait key 모두 처리
- `gameType` 파라미터 추가 (기존 단일 큐 메서드 deprecated 유지)

#### 2-4. UpdateMatchResultAsync
- ratingDiff = `Math.Abs(record.Player1Rating - record.Player2Rating)`
- kMultiplier = `ratingDiff switch { <=200 → 1.0, <=400 → 0.5, <=800 → 0.25, _ → 0.125 }`
- `_ = UpdateEloRatingsAsync(...)` → `await UpdateEloRatingsAsync(..., kMultiplier)`

#### 2-5. UpdateEloRatingsAsync 시그니처
- `kMultiplier = 1.0` 기본값 파라미터 추가
- `k1 = (totalGames1 < 300 ? 32.0 : 16.0) * kMultiplier`

### 3. GameMatchController.cs 변경
- `CancelMatch` 엔드포인트: `[FromQuery] string gameType = "gomoku"` 추가
- `_matchService.CancelMatchAsync(userId, gameType)` 호출

### 4. GameMatchControllerTests.cs — ELO 통합 테스트 5개 추가
- 기존 MatchingTestWebAppFactory 패턴 재사용 (InMemory EF Core DB)
- `ReportMatchResult_Player1Wins_IncreasesPlayer1Rating`
- `ReportMatchResult_Player2Wins_IncreasesPlayer2Rating`
- `ReportMatchResult_Draw_BothRatingsAdjustedAndDrawCountIncremented`
- `ReportMatchResult_LargeRatingDiff_SmallerEloChange`
- `ReportMatchResult_NewPlayer_WinCountAndRatingUpdated`
- UpdateEloRatingsAsync가 awaited이므로 결과 즉시 DB 반영 → 조회 가능

### 5. DummyClient 보강
- 매칭 재시도 로직: 202(대기) 수신 시 5초 대기 후 재시도 (최대 24회)
- 타임아웃 시 `matchTimeout++` 집계
- 리포트 항목 추가: `matchTimeout`, `avgRatingDiff`, `expandedMatches`

## 영향 범위 (예상)

| 파일 | 변경 종류 |
|------|---------|
| `PlatformA.Library/Common/Consts.cs` | 상수 4개 추가 |
| `PlatformA.Matching.API/Services/GameMatchService.cs` | 핵심 로직 변경 |
| `PlatformA.Matching.API/Controllers/GameMatchController.cs` | 파라미터 추가 |
| `PlatformA.Tests.Matching.API/GameMatchControllerTests.cs` | 테스트 5개 추가 |
| `PlatformA.Game.DummyClient/` (복수 파일) | 재시도 + 리포트 |

신규 파일 없음.

## 제약 및 주의사항

- ADR-006: Redis SortedSet + Lua 스크립트 기반 매칭 — score 변경만이므로 ADR 준수
- ADR-001: Redis 키 상수는 Consts.cs에서만 관리 — `MATCH_WAIT_KEY_PREFIX` 반드시 추가
- TTL 없는 Redis 키 금지 — wait key는 SETEX(120s) 필수
- UpdateEloRatingsAsync awaitable 전환은 fire-and-forget 제거 → 기존 호출부 전체 확인
- Matching.API 테스트: Reflection 주입 패턴 + InMemory EF Core (tests.md 준수)

## 구현 접근 방향

1. Consts.cs 상수 추가 (단순)
2. GameMatchService Lua 스크립트 2개로 분리
3. TryMatchAsync: GetPlayerRatingAsync → 3단계 → 큐 진입
4. 내부 헬퍼 메서드: `TryStrictMatchAsync`, `TryExpandedMatchAsync`, `FinalizeMatchAsync`
5. UpdateEloRatingsAsync kMultiplier 추가 (기존 호출부: 기본값 1.0으로 하위호환)
6. UpdateMatchResultAsync에서 kMultiplier 계산 후 await
7. 컨트롤러 파라미터 추가 (1줄)
8. 테스트 5개: InMemory DB에 PlayerRating + MatchRecord 시드 → POST result → DB 재조회
9. DummyClient: 재시도 루프 추가 + JSON 필드 추가

## 검증 기준

- `dotnet build PlatformA.sln` 오류 0개
- `dotnet test PlatformA.sln -q` 246 → 251개 통과 (Matching.API 28 → 33)
- ELO 테스트 5개: DB PlayerRating 변동, WinCount/DrawCount 변경 단언
- DummyClient: `matchTimeout` 필드 JSON에 포함
