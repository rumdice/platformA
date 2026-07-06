# 요구사항 명세: RatingDbFallback

작성일: 2026-07-06
브랜치: 2026-07-06_RatingDbFallback
소스: 사용자 직접 작성 (sprint-rating-db-fallback-plan-2026-07-06.md)

## 요구사항 요약

`GameMatchService.GetPlayerRatingAsync`가 Redis miss 시 DB PlayerRatings를 fallback 조회하도록 개선한다.
DB hit 시 Redis에 1시간 TTL로 재캐싱하여 다음 요청부터 캐시 적중되게 한다.
DB miss(신규 유저)는 기존처럼 DEFAULT_PLAYER_RATING(1000)을 반환한다.

## 상세 요구사항

### 1. GetPlayerRatingAsync 변경

변경 전:
```text
Redis hit  → 반환
Redis miss → DEFAULT_PLAYER_RATING(1000) 반환
```

변경 후:
```text
Redis hit  → 즉시 반환 (DB 미조회)
Redis miss → DB PlayerRatings.FindAsync(userId)
  DB hit   → (int)playerRating.Rating 계산 → Redis StringSet(TTL 1h) → 반환
  DB miss  → DEFAULT_PLAYER_RATING(1000) 반환
```

### 2. Redis 재캐싱 TTL

- `TimeSpan.FromHours(1)` — 기존 UpdateEloRatingsAsync와 동일 정책 유지

### 3. double → int 변환

- `(int)playerRating.Rating` — 기존 코드와 동일 기준 (단순 cast)

### 4. 테스트 4개 추가

- `GetPlayerRating_RedisHit_ReturnsRedisValue` — Redis에 값이 있으면 DB 미조회하고 Redis 값 반환
- `GetPlayerRating_RedisMiss_DbHit_ReturnsDbValue` — Redis miss, DB에 rating이 있으면 DB 값 반환
- `GetPlayerRating_RedisMiss_DbHit_RecachesInRedis` — DB fallback 후 Redis에 재캐싱 확인
- `GetPlayerRating_RedisMiss_DbMiss_ReturnsDefault` — Redis/DB 모두 없으면 1000 반환

## 영향 범위 (예상)

| 파일 | 변경 종류 |
|------|---------|
| `PlatformA.Matching.API/Services/GameMatchService.cs` | 메서드 수정 (약 10줄 추가) |
| `PlatformA.Tests.Matching.API/GameMatchServiceRatingTests.cs` | 신규 파일 (테스트 4개) |

## 제약 및 주의사항

- Redis hit 시 DB 미조회 유지 — 매칭 요청 경로의 DB 부하 방지
- `IDbContextFactory<DbWebAppContext>` 사용 — DbContext 직접 주입 금지
- 기존 PlayerRating 엔티티 타입 그대로 사용 (`dbContext.PlayerRatings.FindAsync`)
- Redis 키 상수는 이미 `Consts.PLAYER_RATING_KEY_PREFIX`로 등록됨 — 변경 불필요

## 구현 접근 방향

1. `GameMatchService.GetPlayerRatingAsync` 수정: Redis 조회 후 miss이면 dbFactory로 DbContext 생성 → FindAsync → 재캐싱
2. 테스트: 기존 MatchingTestWebAppFactory 패턴 재사용, Reflection으로 Redis Mock 주입, InMemory EF Core DB에 PlayerRating 시드

## 검증 기준

- `dotnet build PlatformA.sln` 오류 0개
- `dotnet test PlatformA.sln -q` 전체 통과 (251 → 255개)
- GetPlayerRating 테스트 4개 모두 통과

## DESIGN_REVIEW 결과

| ADR | 관련 여부 | 판정 |
|-----|---------|------|
| ADR-001 (Redis 키 Consts.cs 관리) | 관련 있음 | 준수 — 신규 키 없음, 기존 PLAYER_RATING_KEY_PREFIX 사용 |
| ADR-006 (Redis SortedSet 매칭) | 관련 있음 | 준수 — TryMatchAsync 변경 없음, GetPlayerRatingAsync 내부만 수정 |

판정: ✅ 기존 ADR 준수 — cache-aside fallback 패턴은 표준 구현, 신규 ADR 불필요
