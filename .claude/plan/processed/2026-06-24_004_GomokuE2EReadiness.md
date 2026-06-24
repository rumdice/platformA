# 요구사항 명세: GomokuE2EReadiness

작성일: 2026-06-24
브랜치: 2026-06-24_GomokuE2EReadiness
소스: .claude/plan/2026-06-24_GomokuE2EReadiness.md

## 요구사항 요약

Gomoku E2E 실행 전 코드에서 발견된 버그 5건(Redis publish 미보호, 구 매칭 루프 활성, gameType 미검증, async void 분리 미구현, 무승부 표시 오류)을 수정하고, 두 플레이어가 로비에서 매칭되어 오목 게임을 완주하는 전체 흐름을 자동 검증하는 TwoPlayerGomokuScenario를 DummyClient에 추가한다.

## 상세 요구사항

### P0-A. Redis publish try/catch 추가 (GameMatchService.TryMatchAsync)

파일: `PlatformA.Matching.API/Services/GameMatchService.cs`

`TryMatchAsync` 내 publish 구간(line 149-152)을 try/catch로 보호한다.
실패 시 userId, opponentId, roomId, gameType을 포함한 Error 로그를 기록한다.

### P0-B. 구 BackgroundService 매칭 루프 비활성화 (GameMatchService)

파일: `PlatformA.Matching.API/Services/GameMatchService.cs`

`ExecuteAsync`에서 `ProcessQueueAsync` 호출을 제거한다.
stale Pending 정리(`AbandonStaleMatchesAsync`)는 유지한다.
dead code가 되는 `ProcessQueueAsync`, `ProcessMatchingAsync` private 메서드를 제거한다.
`BrokenCircuitException` 분기도 ExecuteAsync에서 제거한다 (ProcessQueueAsync가 Redis 호출을 하고 있었으므로).
`AddPlayerToQueueAsync`, `RemovePlayerFromQueueAsync`, `GetQueueRankAsync`, `GetQueueLengthAsync` public 메서드가 현재 외부에서 호출되는지 확인 후 판단:
- 외부 호출 없으면: deprecated 주석 추가 (삭제 시 Breaking Change 위험이 있으므로 주석만)
- 외부 호출 있으면: 유지하고 현황을 코멘트에 기록

### P0-C. gameType 검증 추가 (GomokuPacketHandler)

파일: `PlatformA.Game.Gomoku/Packet/GomokuPacketHandler.cs`

`ProcessLoginAsync`에서 roomId 파싱 직후 `gameType` 필드를 검증한다.
"gomoku"가 아닌 경우 `LoginNotInQueue` 코드로 응답 후 Disconnect.

### P1-D. MatchNotificationService ProcessMatchFoundAsync 분리

파일: `PlatformA.Game.Lobby/Services/MatchNotificationService.cs`

`OnMatchFound(async void)` → 얇은 래퍼만 남기고 실제 로직을 `internal async Task ProcessMatchFoundAsync(RedisValue message)`로 분리한다. 기존 try/catch는 `ProcessMatchFoundAsync` 내부에 유지한다.

### P1-E. MatchHistory 무승부 표시 오류 수정

파일: `PlatformA.Matching.API/Services/GameMatchService.cs`

`GetMatchHistoryAsync`의 Result 결정 로직을 수정한다:
- `Status != Completed` → "미완료"
- `Status == Completed && WinnerId == null` → "무승부"  
- `Status == Completed && WinnerId == userId` → "승리"
- 그 외 → "패배"

### P1-F. DummyClient — TwoPlayerGomokuScenario (시나리오 9)

신규 파일: `PlatformA.Game.DummyClient/Scenarios/TwoPlayerGomokuScenario.cs`
수정 파일: `PlatformA.Game.DummyClient/Program.cs`

두 개의 Task를 병렬로 실행하여 전체 E2E 흐름을 자동 검증한다:

```
[Player 1 Task]                           [Player 2 Task]
1. Auth.API 로그인                        1. Auth.API 로그인
2. Lobby SignalR 연결 + MatchFound 핸들러 2. Lobby SignalR 연결 + MatchFound 핸들러
3. RequestMatch("gomoku") 호출            3. RequestMatch("gomoku") 100ms 후 호출
4. MatchFound 수신 (host/port/roomId)     4. MatchFound 수신
5. Gomoku TCP 접속                        5. Gomoku TCP 접속
6. CLogin 전송 (JWT)                      6. CLogin 전송 (JWT)
7. SLogin 성공 확인                       7. SLogin 성공 확인
8. SGameStart 수신 확인
           ↓ 양쪽 SGameStart 완료 후 교대 자동 진행
9. CPlaceStone 교대 자동 진행 (FirstTurnPlayerId 기준)
10. SBoardUpdate 수신 확인
11. SGameOver 수신 + lobbyUrl 출력
12. GET /api/gamematch/history 로 MatchRecord Completed 확인
13. 성공/실패 판정 출력
```

세부 구현:
- `TaskCompletionSource<MatchFoundInfo>` per player로 MatchFound 수신 동기화
- CPlaceStone 좌표: `(7,7)`, `(7,8)`, `(8,7)`, `(8,8)` ... 순차 증가 (승리 조건 충족 시까지)
- 각 대기 구간 최대 30초 타임아웃
- SGameOver의 LobbyUrl 출력 및 Reason 출력
- Program.cs에 "9. [시나리오 9] 두 명 자동 매칭 → Gomoku 게임 완주 E2E 검증" 추가

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|------|---------|
| `PlatformA.Matching.API/Services/GameMatchService.cs` | publish try/catch, 구 루프 제거, Draw 수정 |
| `PlatformA.Game.Gomoku/Packet/GomokuPacketHandler.cs` | gameType 검증 추가 |
| `PlatformA.Game.Lobby/Services/MatchNotificationService.cs` | ProcessMatchFoundAsync 분리 |
| `PlatformA.Game.DummyClient/Scenarios/TwoPlayerGomokuScenario.cs` | 신규 생성 |
| `PlatformA.Game.DummyClient/Program.cs` | 시나리오 9 메뉴 추가 |

## 제약 및 주의사항

- ADR-006(매칭 개선): 신 흐름 TryMatchAsync(gameType) 유지 — 변경 없음
- ADR-011(Lobby SignalR): MatchNotificationService 인터페이스 변경 없음 — 내부 리팩터만
- ADR-007(Protobuf): GomokuPacketHandler 수정 시 수동 직렬화 금지 유지
- `ProcessMatchingAsync` 제거 시 `RecordMatchStartAsync(int, int)` 오버로드도 호출처가 없으면 함께 제거
- TwoPlayerGomokuScenario는 모든 로컬 서비스가 기동된 상태에서만 동작

## 구현 접근 방향

1. GameMatchService — P0-A(publish try/catch) + P0-B(구 루프/메서드 제거) + P1-E(Draw 수정) 묶어 처리
2. GomokuPacketHandler — P0-C(gameType 검증) 단독 수정
3. MatchNotificationService — P1-D(메서드 분리) 단독 수정
4. TwoPlayerGomokuScenario + Program.cs — 시나리오 신규 작성
5. 테스트: GameMatchService(Draw 수정, publish 실패 시 로그), MatchNotificationService(ProcessMatchFoundAsync)
6. dotnet build → dotnet test 검증

## 검증 기준

- `dotnet build PlatformA.sln` 오류 0개
- `dotnet test PlatformA.sln` 전체 통과
- GameMatchService에서 publish 실패 시 LogError 호출됨 (테스트)
- GetMatchHistoryAsync에서 Draw 완료 매치가 "무승부"로 반환됨 (테스트)
- MatchNotificationService.ProcessMatchFoundAsync가 독립적으로 테스트 가능
- 시나리오 9 실행 시 두 플레이어가 자동으로 매칭→게임→종료 흐름 완주
