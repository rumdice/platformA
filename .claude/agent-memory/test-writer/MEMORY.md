# test-writer 에이전트 메모리

세션 간 학습한 테스트 패턴과 엣지 케이스를 누적한다.

---

## 팩토리 패턴 요약

### Auth.API — Reflection 주입 패턴
- `RedisManager`는 private 생성자 싱글톤 → 서브클래싱 불가
- `FieldInfo.SetValue`로 `_redis`, `_pipeline` 필드를 직접 교체
- `ScriptEvaluateAsync` 기본 반환값: `RedisResult.Create(1L)` (Rate Limit 통과 + 토큰 유효)
- Rate Limit 정책은 `permitLimit: 1000`으로 재등록하여 테스트 차단 방지

### Utils.API — 직접 교체 패턴
- `IConnectionMultiplexer`를 DI로 직접 받으므로 Reflection 불필요
- `services.Remove` + `services.AddSingleton(MockRedis.Object)` 패턴
- `IHostedService` 전체 제거 (StatSyncsService 등 배경 서비스가 실제 Redis 연결 시도)
- DB: SQLite InMemory `Data Source={Guid.NewGuid():N}.db`

---

## 발견된 엣지 케이스

_(에이전트가 새 케이스를 발견할 때마다 이 섹션에 추가)_

---

## 테스트 커버리지 현황

| 프로젝트 | 팩토리 | 테스트 파일 |
|----------|--------|------------|
| Auth.API | `AuthTestWebAppFactory` | `AuthControllerTests.cs`, `AuthModelValidationTests.cs` |
| Utils.API | `TestWebAppFactory` | `UtilControllerTests.cs`, `Base62ConverterTests.cs`, `SnowflakeGeneratorTests.cs` |
