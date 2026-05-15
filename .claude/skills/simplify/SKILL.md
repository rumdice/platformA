---
name: simplify
description: PlatformA PATTERNS.md 기준으로 변경된 코드의 품질을 검토하고 개선한다. 패킷 직렬화, DI, Redis 래핑, EF Core 패턴 위반을 찾아 수정한다.
---

# PlatformA 코드 품질 개선

## 현재 변경사항
- 변경 파일: !`git diff --name-only HEAD~1 2>/dev/null || git diff --name-only --cached`

대상 파일 또는 범위: $ARGUMENTS

---

## 검토 기준 (rules/patterns.md 자동 로드됨)

### 패킷 코드 (ADR-007: Protobuf)
- `BitConverter`, `BinaryReader`, `BinaryWriter` 등 수동 직렬화 코드가 있으면 Protobuf 방식으로 교체
- `room.Push()` 밖에서 게임 상태를 수정하는 코드 → `room.Push()` 안으로 이동
- `[Packet]` 어트리뷰트, `partial struct`, `Size` 상수는 더 이상 사용하지 않음 — 제거 대상

### DI / 서비스 구조
- `new` 키워드로 직접 생성하는 서비스 인스턴스 → 생성자 DI로 교체
- `DbContext`를 직접 생성하는 코드 → `IDbContextFactory<T>` 방식으로 교체
- `Singleton`/`Scoped`/`Transient` 생명주기가 서비스 역할에 맞는지 확인

### Redis 사용
- `RedisManager.Instance.ExecuteAsync()` 래핑 없이 직접 호출하는 코드 → 래핑 적용
- 하드코딩된 Redis 키 문자열 → `Consts.cs` 상수로 추출
- TTL 없는 `StringSetAsync` 호출 → TTL 추가
- `BrokenCircuitException` 미처리 구간 → 예외 처리 추가

### 설정/상수
- 코드 내 매직 넘버/문자열 → `Consts.cs` 상수로 추출
- `appsettings.json`에 상수값이 추가되어 있으면 → `Consts.cs`로 이동

### 에러 응답
- `{ Message = "..." }` 포맷을 따르지 않는 에러 응답 → 통일
- 예외를 단순히 `ex.Message`로만 반환하는 경우 → 로깅 추가

### 불필요한 복잡도
- 동일한 Redis 호출 로직이 반복되면 → 메서드 추출
- `try-catch`가 과도하게 중첩된 경우 → 정리
- `async/await` 없이 `.Result` 또는 `.Wait()` 사용 → `await`으로 교체

---

위 기준으로 변경된 코드를 검토하고, 문제가 있는 부분을 직접 수정한다.
수정 후 `dotnet build PlatformA.sln`으로 빌드를 확인한다.
