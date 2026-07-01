# 요구사항 명세: FixTaskRunSilentException

작성일: 2026-07-01
브랜치: 2026-07-01_FixTaskRunSilentException
소스: /plan 단계 작업 설명

## 요구사항 요약
Game.Gomoku에서 `_ = Task.Run(...)` 패턴 내부 예외가 로그 없이 사라지는 두 곳을 수정한다.
Program.cs 헬스체크 서버 초기화 실패와 GomokuRoom.cs 타임아웃 루프 예외를 ILogger로 기록한다.

## 상세 요구사항

1. **Program.cs — 헬스체크 Task.Run 외부 try-catch 추가**
   - 현재: `httpListener.Start()` 등 초기화 실패 시 예외가 무음으로 사라짐
   - 수정: `loggerFactory.CreateLogger("Gomoku.HealthCheck")`로 logger 생성 후,
     Task.Run 내부 전체를 try-catch로 감싸 초기화 실패를 `LogCritical`로 기록
   - 기존 내부 catch의 `Console.WriteLine` → `logger.LogError(ex, ...)`로 교체

2. **GomokuRoom.cs — 타임아웃 루프 try-catch 추가**
   - 현재: 타임아웃 루프 `while(GameState == InProgress)` 전체에 try-catch 없음
   - 수정: 루프 전체를 try-catch로 감싸 `_logger?.LogError(ex, ...)`로 기록
   - ILogger 주입: `private static ILogger? _logger;` + `internal static void SetLogger(ILogger l)`
   - Program.cs에서 `GomokuRoom.SetLogger(loggerFactory.CreateLogger<GomokuRoom>())` 호출

## 영향 범위 (예상)
- `PlatformA.Game.Gomoku/Program.cs` — logger 생성 + Task.Run 외부 try-catch
- `PlatformA.Game.Gomoku/Core/GomokuRoom.cs` — static ILogger + SetLogger + 타임아웃 루프 try-catch

## 제약 및 주의사항
- ILogger는 기존 `loggerFactory`를 재사용한다 — 새 LoggerFactory 인스턴스 생성 금지
- `Console.WriteLine` → `ILogger` 전환은 Task.Run 내 예외 경로만 대상 (전체 리팩토링 아님)
- `GomokuRoom` 생성자 시그니처 변경 없음 — 기존 테스트 영향 최소화
- static setter 패턴 사용 이유: GomokuRoom은 DI 외부에서 생성(`new GomokuRoom(id)`)되므로

## 구현 접근 방향
1. `Program.cs`에서 `loggerFactory.CreateLogger` 호출을 확장하여 두 개의 logger 변수 준비
2. `GomokuRoom.SetLogger(...)` 호출을 Program.cs의 기존 Init 코드 바로 뒤에 추가
3. `GomokuRoom.cs`에 `private static ILogger? _logger` 필드와 `SetLogger` 메서드 추가
4. 타임아웃 루프 전체를 `try { ... } catch (Exception ex) { _logger?.LogError(...) }` 로 감쌈
5. Program.cs Task.Run 내부 전체를 외부 try-catch로 감싸고 기존 inner catch 개선

## 검증 기준
- `dotnet build PlatformA.sln` 오류 0개
- `dotnet test PlatformA.sln` 전체 통과
- `GomokuRoom`의 타임아웃 루프 내에서 강제 예외 발생 시 LogError 로그가 출력됨 (코드 리뷰로 확인)
- Program.cs에서 `httpListener.Start()` 실패 시 LogCritical이 호출됨 (코드 리뷰로 확인)
