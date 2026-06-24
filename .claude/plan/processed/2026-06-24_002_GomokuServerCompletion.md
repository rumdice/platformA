# 요구사항 명세: GomokuServerCompletion

작성일: 2026-06-24
브랜치: 2026-06-24_GomokuServerCompletion
소스: .claude/plan/2026-06-24_GomokuCompletion_analysis.md

## 요구사항 요약

Game.Gomoku 서버의 미구현 핵심 기능(턴 타임아웃 루프, 무승부 처리, 방 메모리 정리, 결과 기록)을 완성하고
레거시 코드를 제거하여 전체 게임 플로우(로그인→매칭→배틀→로비복귀) 완성도를 100%로 달성한다.
P2로 SGameOver lobbyUrl 필드와 Dockerfile + 헬스체크도 추가한다.

## 상세 요구사항

### P0-A. 턴 타임아웃 백그라운드 루프 (GomokuRoom)

`TurnManager.IsTimeout()` 메서드는 구현됐지만 호출자가 없어 플레이어가 무한정 아무것도 하지 않아도 게임이 진행된다.

- `GomokuRoom.StartGame()` 내부에서 백그라운드 Task를 시작한다.
- 1초 간격으로 `Push()` 안에서 `TurnManager.IsTimeout()`을 호출한다.
- 타임아웃이면 비활성 플레이어 상대방이 승리 → `FinishGame(winnerId, GameOverReason.Timeout)` 호출.
- 게임 상태가 `InProgress`가 아니면 루프를 종료한다.

```csharp
// GomokuRoom.StartGame() 끝에 추가
_ = Task.Run(async () =>
{
    while (GameState == GomokuGameState.InProgress)
    {
        await Task.Delay(1000);
        Push(() =>
        {
            if (GameState != GomokuGameState.InProgress) return;
            if (_turn != null && _turn.IsTimeout())
            {
                int winner = _turn.GetOpponentId(_turn.CurrentTurnPlayerId);
                FinishGame(winner, GameOverReason.Timeout);
            }
        });
    }
});
```

> `TurnManager`에 `GetOpponentId(int currentPlayerId)` 헬퍼가 없으면 추가한다.

### P0-B. 무승부 처리 — Board.IsFull() + proto DRAW

225칸이 다 채워져도 게임이 종료되지 않는다.

- `Board.cs`에 `IsFull()` 메서드를 추가한다.
- `packets.proto`의 `GameOverReason` enum에 `DRAW = 3` 추가.
- `GomokuRoom.HandlePlaceStone()` 내 WinChecker 판정 후 `_board.IsFull()` 체크 → `FinishGame(0, GameOverReason.Draw)` 호출.

```csharp
// Board.cs
public bool IsFull() => _cells.Cast<StoneColor>().All(c => c != StoneColor.StoneNone);

// packets.proto
enum GameOverReason {
  FIVE_IN_ROW = 0;
  DISCONNECT = 1;
  TIMEOUT = 2;
  DRAW = 3;
}
```

### P1-C. 게임 종료 후 방 메모리 정리

`FinishGame()` 호출 후 `GomokuRoomManager.Remove(roomId)`가 호출되지 않아 방이 메모리에 영원히 축적된다.

- `GomokuRoom` 생성자에 `string roomId` 파라미터 추가 → `_roomId` 필드 저장.
- `GomokuRoomManager.GetOrCreate()`에서 roomId를 생성자에 전달.
- `FinishGame()` 마지막에 `GomokuRoomManager.Instance.Remove(_roomId)` 호출.

### P1-D. MatchRecord 결과 업데이트 API

매칭 시작 시 `MatchRecord(Status=InProgress)`는 기록되지만 게임 종료 후 결과가 업데이트되지 않는다.

- Matching.API에 `POST /api/gamematch/result` 엔드포인트 추가.
- Request: `{ RoomId: string, WinnerId: int, Reason: string }` (WinnerId=0이면 무승부)
- `MatchRecord.WinnerId`, `MatchRecord.Status = Finished`, `MatchRecord.FinishedAt` 업데이트.
- Game.Gomoku의 `FinishGame()` 에서 HTTP POST로 결과를 보고한다.
- 인증: JWT 불필요 — 내부 서비스 간 통신 (내부 네트워크만 노출).

### P1-E. Program.cs 레거시 핸들러 제거

`Game.Gomoku/Program.cs`의 `OnMatchSuccessReceived` 핸들러는 구 `channel:match_success`를 구독하며
현재 플로우(`channel:match_found`)와 무관하다. 데드 코드이므로 제거한다.

```csharp
// 제거 대상 (Program.cs)
RedisManager.Instance.OnMatchSuccessReceived += (matchEvent) =>
{
    GameRoomManager.Instance.CreateRoom(matchEvent.RoomId);
};
```

### P2-F. SGameOver에 lobbyUrl 필드 추가

게임 종료 후 클라이언트가 SignalR 로비로 돌아가야 하지만 명시적 신호가 없다.

- `packets.proto`의 `SGameOver` 메시지에 `string lobby_url = 4;` 필드 추가.
- `GomokuRoom.FinishGame()`에서 `LobbyUrl = Consts.LOBBY_SERVER_URL` 설정.
- `Consts.cs`에 `LOBBY_SERVER_URL` 상수 추가.

### P2-G. Dockerfile + 헬스체크

Game.Gomoku는 다른 서비스와 달리 Dockerfile과 헬스체크가 없다.

- 기존 서비스(Auth.API, Matching.API 등) Dockerfile 패턴을 그대로 사용.
- `Program.cs`에 `/healthz` (liveness) 엔드포인트 추가.
  - Game.Gomoku는 TCP + ASP.NET Core 혼합 → ASP.NET Core 웹 서버가 이미 있는지 확인 후 추가.

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|------|---------|
| `PlatformA.Game.Gomoku/Core/GomokuRoom.cs` | 타임아웃 루프, IsFull 체크, 방 정리, 결과 보고 HTTP |
| `PlatformA.Game.Gomoku/Core/TurnManager.cs` | GetOpponentId() 헬퍼 추가 |
| `PlatformA.Game.Gomoku/Core/Board.cs` | IsFull() 추가 |
| `PlatformA.Game.Gomoku/Core/GomokuRoomManager.cs` | GetOrCreate()에 roomId 전달 |
| `PlatformA.Game.Gomoku/Program.cs` | 레거시 핸들러 제거, 헬스체크 추가 |
| `PlatformA.Game.Gomoku/Dockerfile` | 신규 생성 |
| `PlatformA.Library/Packets/Proto/packets.proto` | GameOverReason.DRAW=3, SGameOver.lobby_url 추가 |
| `PlatformA.Library/Common/Consts.cs` | LOBBY_SERVER_URL 상수 추가 |
| `PlatformA.Matching.API/Controllers/GameMatchController.cs` | POST /api/gamematch/result 추가 |
| `PlatformA.Matching.API/Services/GameMatchService.cs` | UpdateMatchResultAsync() 추가 |
| `PlatformA.Tests.Game.Gomoku/` | 테스트 케이스 추가 |

## 제약 및 주의사항

- **ADR-007 (Protobuf)**: 패킷 수정은 `packets.proto`에서만. 수동 직렬화 절대 금지.
- **ADR-005**: proto3 기본값 주의 — `FIVE_IN_ROW = 0` (기본값 wire 제외). DRAW, TIMEOUT은 0이 아닌 값으로 설정해야 한다.
- **room.Push() 패턴**: 모든 게임 상태 수정은 `Push()` 안에서만 실행 (타임아웃 루프 포함).
- **서비스 경계**: Gomoku가 MySQL DB에 직접 접근하지 않는다 — Matching.API HTTP POST로만 결과 보고.
- **내부 HTTP 인증**: Gomoku→Matching.API는 내부 네트워크 통신 — JWT 불필요, IP 기반 접근 제어만.
- **FinishGame 중복 호출 방지**: 타임아웃 루프 + 돌 놓기 + 연결 끊김 3곳에서 FinishGame이 호출될 수 있다. `GameState != InProgress` 체크로 중복 실행을 방지한다.

## 구현 접근 방향

1. **proto 먼저**: `packets.proto` 수정(DRAW enum + lobbyUrl) → protobuf 자동 생성 코드 반영 확인.
2. **Board/TurnManager 유틸**: `IsFull()`, `GetOpponentId()` 단순 메서드 추가.
3. **GomokuRoom 핵심 수정**: P0-A(타임아웃 루프), P0-B(무승부), P1-C(방 정리), P1-D(결과 HTTP) 순서로.
4. **Matching.API result 엔드포인트**: 단순 DB 업데이트 — EF Core `FindAsync` + field update + `SaveChangesAsync`.
5. **Program.cs 정리**: 레거시 핸들러 제거 + 헬스체크 추가 (ASP.NET Core WebApplication 이미 있는지 확인).
6. **Dockerfile**: 기존 패턴 복사 후 포트/프로젝트명 수정.
7. **테스트**: GomokuRoom 타임아웃·무승부·방 정리, Matching.API result endpoint 케이스.

## 검증 기준

- `dotnet build PlatformA.sln` 오류 0개
- `dotnet test PlatformA.sln` 전체 통과 (신규 테스트 포함)
- `TurnManager.IsTimeout()` 반환 시 `FinishGame` 호출됨 (테스트로 검증)
- 225칸 가득 채울 때 `GameOverReason.Draw` 로 종료됨 (테스트로 검증)
- `FinishGame` 호출 후 `GomokuRoomManager`에서 방이 제거됨 (테스트로 검증)
- `POST /api/gamematch/result` 200 OK + MatchRecord 상태 업데이트됨 (테스트로 검증)
- Game.Gomoku `Dockerfile` 존재 + `/healthz` 200 응답
