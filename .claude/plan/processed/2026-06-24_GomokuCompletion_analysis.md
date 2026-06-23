# 오목 게임서버 완성도 분석 — 2026-06-23

작성일: 2026-06-23  
분석 기준: Sprint #69·#70 완료 후 전체 플로우 코드 리뷰

---

## 목표 플로우

```
클라이언트
  │
  ▼  [1] JWT 로그인
Game.Lobby (SignalR :7777)
  │  RequestMatch Hub 메서드
  ▼  [2] 매칭 신청
Matching.API
  │  TryMatchAsync → game_transfer 티켓 발급
  │  Redis channel:match_found publish (두 플레이어 모두)
  ▼  [3] 매칭 결과 수신
MatchNotificationService
  │  SignalR MatchFound 이벤트 push (host, port, roomId)
  ▼  [4] 게임 서버 접속
Game.Gomoku TCP (:7778)
  │  CLogin → JWT 검증 + game_transfer 티켓 소비 → GomokuRoom 입장
  │  CPlaceStone → 돌 놓기 → SBoardUpdate broadcast
  │  WinChecker 5연속 → SGameOver
  ▼  [5] 게임 종료 후 로비 복귀
Game.Lobby (SignalR)
```

---

## 구간별 완성도

| 구간 | 완성도 | 상태 |
|------|--------|------|
| [1] 로그인 → 매칭 신청 | 95% | ✅ Game.Lobby SignalR + JWT 완비 |
| [2] Matching.API TryMatchAsync | 95% | ✅ gameType별 큐, Lua 원자적 pop, MMR 조회 |
| [3] 매칭 결과 수신 (SignalR push) | 95% | ✅ MatchNotificationService 정상 작동 |
| [4] 오목 서버 게임 로직 | 75% | ⚠️ 핵심 로직 완성, 타임아웃·방 정리 누락 |
| [5] 게임 종료 → 로비 복귀 | 30% | ❌ 복귀 신호·흐름 미구현 |

**전체 완성도: ~65%**

---

## 잘 구현된 부분

| 항목 | 파일 |
|------|------|
| game_transfer 티켓 검증·소비 (1회용) | `GomokuPacketHandler.cs` |
| JWT 인증 + 중복 로그인 락 | `GomokuPacketHandler.cs` |
| 15×15 Board + 범위/중복 검사 | `Board.cs` |
| TurnManager (흑선, 교대) | `TurnManager.cs` |
| WinChecker (4방향 5연속) | `WinChecker.cs` |
| JobQueue 기반 thread-safe 상태 관리 | `GomokuRoom.cs` (Push 패턴) |
| 연결 끊김 → 상대방 승리 처리 | `GomokuSession.OnDisconnected` |
| GomokuRoomManager (string roomId 기반) | `GomokuRoomManager.cs` |

---

## 반드시 고쳐야 할 버그 / 미구현 항목

### 🔴 P0 — 게임이 정상 진행되지 않는 문제

#### 1. 턴 타임아웃 미작동

`TurnManager.IsTimeout()` (30초 초과 감지) 메서드는 있지만 **호출자가 없다.**  
백그라운드 루프가 없어서 플레이어가 무한정 아무것도 안 해도 게임이 멈춘다.

```csharp
// TurnManager.cs:36 — 있지만 호출자 없음
public bool IsTimeout() =>
    (DateTime.UtcNow - _turnStartedAt).TotalSeconds > TurnTimeoutSeconds;
```

**해결 방향**: `GomokuRoom`에 주기적으로 타임아웃을 체크하는 백그라운드 Task 추가.  
`Push()`로 안전하게 `FinishGame(winnerId, GameOverReason.Timeout)` 호출.

```csharp
// GomokuRoom.cs — StartGame() 내부에서 시작
_ = Task.Run(async () =>
{
    while (GameState == GomokuGameState.InProgress)
    {
        await Task.Delay(1000);
        Push(() =>
        {
            if (_turn != null && _turn.IsTimeout())
            {
                int winner = _turn.GetOpponentId(_turn.CurrentTurnPlayerId);
                FinishGame(winner, GameOverReason.Timeout);
            }
        });
    }
});
```

#### 2. 무승부(보드 가득 참) 미처리

225칸이 다 채워지면 WinChecker가 false를 반환하지만 게임이 종료되지 않는다.  
`HandlePlaceStone` 내에서 Board 전체 채움 여부 체크 후 무승부 처리 필요.

```csharp
// Board.cs에 추가
public bool IsFull() => _cells.Cast<StoneColor>().All(c => c != StoneColor.StoneNone);

// GomokuRoom.HandlePlaceStone 내부
if (WinChecker.CheckWin(_board, x, y, color))
    FinishGame(session.SessionId, GameOverReason.FiveInRow);
else if (_board.IsFull())
    FinishGame(0, GameOverReason.Draw);  // 0 = 무승부
```

> proto 수정 필요: `GameOverReason` enum에 `DRAW = 3` 추가.

### 🟡 P1 — 데이터 정합성 / 운영 문제

#### 3. 게임 종료 후 방 메모리 누수

`FinishGame()` 호출 후 `GomokuRoomManager.Remove(roomId)`가 호출되지 않는다.  
게임이 끝난 방들이 영원히 메모리에 축적된다.

**해결**: `GomokuRoom`이 자신의 `roomId`를 알아야 한다. `GomokuRoomManager.GetOrCreate()`에서  
room 생성 시 roomId를 주입하고, `FinishGame()` 마지막에 `GomokuRoomManager.Instance.Remove(_roomId)` 호출.

#### 4. MatchRecord 승자 미갱신

Matching.API는 매칭 시작 시 `MatchRecord(Status=InProgress)`를 DB에 기록하지만,  
게임 종료 시 `WinnerId` / `Status=Finished` 업데이트하는 코드가 어디에도 없다.

**해결 방향 A** (권장): Gomoku 서버가 게임 종료 시 Matching.API에 HTTP POST로 결과 보고.  
```
POST /api/gamematch/result  { roomId, winnerId, reason }
```
Matching.API가 MatchRecord를 업데이트.

**해결 방향 B**: Gomoku 서버가 직접 DB 업데이트 (서비스 경계 위반 — 비권장).

#### 5. Program.cs 레거시 dead code

```csharp
// Program.cs:20-23 — channel:match_success 구독, 현재 신규 경로와 무관
RedisManager.Instance.OnMatchSuccessReceived += (matchEvent) =>
{
    GameRoomManager.Instance.CreateRoom(matchEvent.RoomId); // 구 int ID 타입
};
```

현재 Lobby → TryMatchAsync 경로는 `channel:match_found`를 사용하고,  
Game.Gomoku Program.cs는 `channel:match_success`를 구독하므로 이 핸들러는 **작동하지 않는다.**  
삭제하거나 `channel:match_found` 구독으로 교체 필요.

### 🟢 P2 — 기능 개선 (선택)

#### 6. 로비 복귀 신호 명시화

`SGameOver` 수신 후 클라이언트가 SignalR 로비로 돌아가야 하는데,  
명시적인 "이제 로비로 가세요" 신호가 없다. 현재는 클라이언트가 `SGameOver` 받으면 알아서 처리해야 한다는 암묵적 규약만 있는 상태.

**해결**: `SGameOver` 패킷에 `LobbyUrl` 필드 추가 또는 별도 `SReturnLobby` 패킷 정의.

#### 7. 재접속 미지원

플레이어가 네트워크 끊김으로 잠깐 연결이 끊기면 즉시 `HandleDisconnect`가 호출되어 상대방이 승리한다.  
실제 서비스라면 짧은 grace period (예: 10초) 후 종료하는 재접속 허용 로직 필요.

#### 8. Dockerfile / 헬스체크 없음

Game.Gomoku는 Dockerfile과 `/healthz` 헬스체크 엔드포인트가 없다.  
K8s 운영 환경을 위해 추가 필요.

---

## 다음 스프린트 작업 범위 제안

### 필수 (P0 + P1) — 규모 M

```
스프린트 제목: GomokuServerCompletion
예상 소요: 1 스프린트

태스크:
  A. 턴 타임아웃 백그라운드 루프 구현 (GomokuRoom)
  B. 무승부 처리 (Board.IsFull + proto Draw enum)
  C. 게임 종료 후 방 메모리 정리 (GomokuRoomManager.Remove)
  D. MatchRecord 결과 업데이트 API 추가 (Matching.API POST /result)
  E. Program.cs 레거시 핸들러 제거
  F. 테스트: 타임아웃·무승부·방 정리 케이스 추가
```

### 선택 (P2)

```
  G. SReturnLobby 패킷 또는 SGameOver에 lobbyUrl 필드 추가
  H. Dockerfile + 헬스체크 추가
```

---

## 참조 파일

| 파일 | 역할 |
|------|------|
| `PlatformA.Game.Gomoku/Core/GomokuRoom.cs` | 게임 상태, 돌 처리, 종료 로직 |
| `PlatformA.Game.Gomoku/Core/TurnManager.cs` | 턴 교대, 타임아웃 감지 |
| `PlatformA.Game.Gomoku/Core/Board.cs` | 15×15 바둑판 |
| `PlatformA.Game.Gomoku/Core/WinChecker.cs` | 5연속 승리 판정 |
| `PlatformA.Game.Gomoku/Core/GomokuRoomManager.cs` | 방 생성·조회·삭제 |
| `PlatformA.Game.Gomoku/Packet/GomokuPacketHandler.cs` | 로그인·돌 놓기 패킷 처리 |
| `PlatformA.Game.Gomoku/Program.cs` | 서버 진입점 (레거시 코드 포함) |
| `PlatformA.Matching.API/Services/GameMatchService.cs` | MatchRecord 저장 위치 |
| `PlatformA.Library/Packets/Proto/packets.proto` | 패킷 정의 (Draw enum 추가 필요) |
