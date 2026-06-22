---
description: xUnit 테스트 작성 규칙 — 통합/유닛 패턴, 팩토리, Mock 설정 강제
globs: ["PlatformA/PlatformA.Tests.*/**"]
---

# 테스트 코딩 규칙

## 메서드 네이밍
- 형식: `{동작}_{조건}_{예상결과}` (예: `Login_ValidCredentials_ReturnsToken`, `Encode_62_Returns10`)
- 한국어 네이밍 금지 — 모두 영어로 작성

## 통합 테스트 구조 (컨트롤러 테스트)
- `IClassFixture<TFactory>` 패턴 필수
- 팩토리 클래스는 `Helpers/` 디렉토리에 위치
- `WebApplicationFactoryClientOptions { AllowAutoRedirect = false }` — 302 직접 검증 시 필수
- 각 테스트 메서드는 독립적이어야 함 (테스트 간 상태 공유 금지)

## Redis Mock 규칙
- Auth.API / Ticketing.API / Matching.API: Reflection 주입 패턴 (`FieldInfo.SetValue`) — `_redis`, `_pipeline` 필드
- Utils.API: 직접 교체 패턴 (`services.Remove` + `services.AddSingleton(MockRedis.Object)`)
- Rate Limit 정책은 `permitLimit: 1000`으로 재등록하여 테스트 차단 방지
- `ScriptEvaluateAsync` 기본 반환값: `RedisResult.Create(1L)` (Rate Limit 통과 + 토큰 유효)
- **Rate Limit 공유 주의**: `ScriptEvaluateAsync`를 일시적으로 오버라이드하는 테스트는 반드시 `It.Is<RedisKey[]>` 키 프레디케이트로 rate limit 키(`rl:*`)를 제외하고, `try/finally`로 복원을 보장해야 함

## DB Mock 규칙
- 실제 InMemory DB 사용 — Moq로 DbContext Mock 절대 금지
- SQLite InMemory: `options.UseSqlite($"Data Source={Guid.NewGuid():N}.db")` (Auth.API, Utils.API)
- EF Core InMemory: `UseInMemoryDatabase(dbName)` + 직접 팩토리 등록 (Matching.API)
  - Pomelo MySQL provider와 충돌 방지: `AddDbContextFactory` 호출 대신 `services.AddSingleton<IDbContextFactory<T>>(new InMemoryDbContextFactory(options))`
- `db.Database.EnsureCreated()` — `CreateHost()` override에서 호출 (SQLite 방식만 해당)

## 테스트 케이스 필수 포함 항목 (컨트롤러)
- 성공 케이스 (200/201): 응답 상태 + JSON 필드 존재 확인
- 입력 검증 실패 (400): DataAnnotation 경계값
- 인증 실패 (401): 잘못된/누락된 토큰
- 비즈니스 규칙 위반: 비밀번호 불일치, 중복 등

## 신규 테스트 작성 시
- `test-writer` 에이전트 활용 권장 (기존 팩토리 패턴 정확히 재사용)
- 새 API 프로젝트 테스트 추가 시 이 파일 현황 테이블 업데이트

## 테스트 프로젝트 현황

| 프로젝트 | 프레임워크 | 테스트 수 | Redis 패턴 | DB 패턴 |
|---------|-----------|---------|-----------|--------|
| `PlatformA.Tests.Auth.API` | net10.0 | 23 | Reflection 주입 | InMemory SQLite |
| `PlatformA.Tests.Utils.API` | net10.0 | 29 | 직접 교체 | InMemory SQLite |
| `PlatformA.Tests.Game.Server` | net10.0 | 56 | — | — |
| `PlatformA.Tests.Ticketing.API` | net10.0 | 13 | Reflection 주입 | 없음 |
| `PlatformA.Tests.Matching.API` | net10.0 | 20 | Reflection 주입 | InMemory EF Core |
| `PlatformA.Tests.Game.Gomoku` | net10.0 | 36 | — | — |
