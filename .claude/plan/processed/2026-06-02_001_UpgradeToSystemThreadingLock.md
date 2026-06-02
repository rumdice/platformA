# 요구사항 명세: UpgradeToSystemThreadingLock

작성일: 2026-06-02
브랜치: 2026-06-02_UpgradeToSystemThreadingLock
소스: plan mode (squishy-greeting-wand.md) + task JSON summary

## 요구사항 요약

.NET 10 환경에서 `private object _lock = new object()` 패턴을 `System.Threading.Lock`으로
교체한다. 전용 Lock 타입은 C# 13+ 컴파일러가 `lock()` 구문을 최적화된 경로로 컴파일하여
GC 압력 감소 및 잠금 획득/해제 성능을 개선한다. 타입 선언 변경만으로 적용되며 기존
`lock(_lock)` 구문은 그대로 유지한다.

## 상세 요구사항

### 1. `PlatformA.Library/Core/JobQueue.cs`
- `private object _lock = new object();` →  `private readonly Lock _lock = new();`
- `readonly` 키워드 추가 (기존 누락 보정)
- `lock(_lock)` 2곳 구문 변경 없음

### 2. `PlatformA.Library/Helper/SnowflakeGenerator.cs`
- `private static readonly object _lock = new object();` → `private static readonly Lock _lock = new();`
- `lock(_lock)` 1곳 구문 변경 없음

### 3. `PlatformA.Library/Network/SessionManager.cs`
- `private readonly object _lock = new object();` → `private readonly Lock _lock = new();`
- `lock(_lock)` 3곳 구문 변경 없음
- `Broadcast()` 내부 `_ = session.SendAsync(packet)` fire-and-forget 구조 유지

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|---|---|
| `PlatformA.Library/Core/JobQueue.cs` | 타입 선언 수정 + readonly 추가 |
| `PlatformA.Library/Helper/SnowflakeGenerator.cs` | 타입 선언 수정 |
| `PlatformA.Library/Network/SessionManager.cs` | 타입 선언 수정 |

서비스 코드·API·DB·Redis 무관. `GameRoom`·`GameRoomManager` 변경 없음.

## 제약 및 주의사항

- `System.Threading.Lock`은 .NET 9+부터 사용 가능 (현재 net10.0 ✅)
- `LangVersion=latest` (C# 13+)이어야 컴파일러 최적화 활성화 — `Directory.Build.props` 확인 ✅
- `DummyClient`는 `LangVersion=13.0` — 해당 파일들은 Library 소속이므로 영향 없음
- `using System.Threading;` 불필요 — `ImplicitUsings=enable`로 전역 using 포함
- `lock(lockObj)` 구문은 Lock 타입과 완전 소스 호환 — 구문 변경 불필요
- async 메서드 내부에서 `lock` 사용 없음 — `using EnterScope()` 패턴 불필요

## 구현 접근 방향

각 파일의 `private [static] [readonly] object _lock = new object();` 선언을
`private [static] readonly Lock _lock = new();` 로 교체한다.
`lock(_lock)` 구문은 일절 변경하지 않는다.

## DESIGN_REVIEW 결과

| ADR | 관련 여부 | 충돌/참고 사항 |
|---|---|---|
| ADR-001 (Redis Cluster) | 관련 없음 | — |
| ADR-007 (Protobuf 패킷) | 관련 없음 | — |
| 그 외 ADR-002~006 | 관련 없음 | — |

**판정: ✅ 기존 ADR 준수 — 신규 ADR 불필요**
내부 구현 세부사항 교체이며 외부 인터페이스·프로토콜·설계 결정 무관.

## 검증 기준

1. `dotnet build PlatformA.sln` 오류 0개
2. `dotnet test PlatformA.sln` 전체 통과 (기존 125개 + test-gen 신규 케이스)
3. SnowflakeGenerator 동시성 테스트 통과
4. GitHub Actions CI 성공
