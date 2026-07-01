# 계획: Gomoku E2E 준비 — 코드 수정 + 전체 흐름 시나리오

작성일: 2026-06-24
근거: platforma_gomoku_e2e_gap_analysis_2026-06-24.md + 코드 직접 검토

---

## 목표

Gap analysis 문서 검토 및 코드 대조 결과, E2E 실행 전에 반드시 수정해야 할 버그 5건과
전체 흐름을 자동으로 검증하는 DummyClient 시나리오 1건을 구현한다.

완료 기준:
> 로컬에서 시나리오 9번 실행 하나로 두 명의 유저가 매칭되고,
> 오목 게임을 완료하며, MatchRecord가 DB에 Completed로 기록된다.

---

## 배경: 코드 검토에서 발견된 실제 이슈

### 이슈 A — Redis publish try/catch 누락 (심각)

`GameMatchService.TryMatchAsync` (line 149-152):

```csharp
// 현재 — try/catch 없음
await _redisManager.GetSubscriber().PublishAsync(..., notifyOpponent);
await _redisManager.GetSubscriber().PublishAsync(..., notifySelf);
```

publish 실패 시: 매칭은 성공, game_transfer 발급 완료, MatchRecord 생성 완료인데
클라이언트는 MatchFound를 받지 못해 게임 진입 불가. 로그도 없음.

### 이슈 B — 구 BackgroundService 루프 활성 상태 (심각)

`GameMatchService : BackgroundService` — `ExecuteAsync`가 200ms마다 `ProcessQueueAsync`를 실행 중.

- 구 흐름 큐: `MATCH_QUEUE_KEY` (단일, gameType 없음)
- 신 흐름 큐: `MATCH_QUEUE_KEY:{gameType}` (TryMatchAsync)
- `ProcessMatchingAsync`에서 **game_transfer를 발급하지 않음** → 이 경로로 매칭되면 Gomoku CLogin에서 티켓 없음으로 접속 불가
- 현재 Lobby는 신 흐름만 호출하므로 구 큐에 유저가 들어가는 경우는 없지만, 루프가 살아있어 혼란 원인

### 이슈 C — gameType 검증 누락 (보안 + E2E 안정성)

`GomokuPacketHandler.ProcessLoginAsync` (line 79-88):
`transferJson`에서 `roomId`만 추출하고 `gameType`은 검증하지 않음.
다른 게임 서버용 transfer 티켓으로 Gomoku에 접속 가능. 다른 게임 서버 추가 시 심각한 혼용.

### 이슈 D — MatchNotificationService async void 분리 미구현

`OnMatchFound`가 `async void`이고 실제 로직이 인라인에 있어 유닛 테스트 불가.
(try/catch는 있으므로 크래시는 방지됨)

### 이슈 E — MatchHistory 무승부 표시 오류

`GameMatchService.GetMatchHistoryAsync` (line 267-270):
```csharp
Result = m.WinnerId == null ? "미완료"   // WinnerId == null = 무승부도 "미완료"로 표시
```
무승부(Draw, Status=Completed, WinnerId=null)가 "미완료"로 잘못 표시됨.

### 이슈 F — DummyClient에 Gomoku E2E 시나리오 없음

기존 MatchingScenario(시나리오 3)는 구 Ticketing API 흐름 기반으로, Lobby SignalR → Gomoku TCP
전체 흐름을 검증하는 자동화 시나리오가 없음.

---

## 구현 명세

### P0-A. Redis publish try/catch 추가

파일: `PlatformA.Matching.API/Services/GameMatchService.cs`

`TryMatchAsync` 내 publish 구간을 try/catch로 감싼다:

```csharp
try
{
    await _redisManager.GetSubscriber().PublishAsync(
        RedisChannel.Literal(Consts.MATCH_FOUND_CHANNEL), notifyOpponent);
    await _redisManager.GetSubscriber().PublishAsync(
        RedisChannel.Literal(Consts.MATCH_FOUND_CHANNEL), notifySelf);
}
catch (Exception ex)
{
    _logger.LogError(
        ex,
        "[Matching] MatchFound publish 실패 — User:{UserId}, Opponent:{OpponentId}, Room:{RoomId}, GameType:{GameType}",
        userId, opponentId, roomId, gameType);
}
```

### P0-B. 구 BackgroundService 매칭 루프 비활성화

파일: `PlatformA.Matching.API/Services/GameMatchService.cs`

`ExecuteAsync`에서 `ProcessQueueAsync` 호출을 제거하고, stale Pending 정리(`AbandonStaleMatchesAsync`)는 유지한다:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    int tickCount = 0;
    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            // ProcessQueueAsync() 호출 제거 — 구 단일 큐 매칭 루프 비활성화
            // 현재 매칭 경로: TryMatchAsync(gameType) — Lobby에서 HTTP 호출
            tickCount++;
            if (tickCount >= 1500)
            {
                tickCount = 0;
                _ = AbandonStaleMatchesAsync();
            }
            await Task.Delay(200, stoppingToken);
        }
        catch (OperationCanceledException) { break; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Matching] 백그라운드 정리 작업 중 예외");
            await Task.Delay(2000, stoppingToken);
        }
    }
}
```

`AddPlayerToQueueAsync`, `RemovePlayerFromQueueAsync`, `GetQueueRankAsync`, `GetQueueLengthAsync`는
현재 어디서도 외부 호출되지 않으므로 그대로 둔다 (삭제 시 Breaking Change 위험).
`ProcessQueueAsync`, `ProcessMatchingAsync`는 private이므로 내부에서만 참조됨 — 호출이 제거되면 dead code가 됨.
컴파일 경고를 없애기 위해 두 메서드를 제거한다.

### P0-C. gameType 검증 추가 (GomokuPacketHandler)

파일: `PlatformA.Game.Gomoku/Packet/GomokuPacketHandler.cs`

`ProcessLoginAsync`에서 roomId 파싱 직후 `gameType` 필드를 검증한다:

```csharp
using (JsonDocument doc = JsonDocument.Parse(transferJson))
{
    JsonElement root = doc.RootElement;
    roomId = root.GetProperty("roomId").GetString() ?? string.Empty;

    // gameType 검증: 이 서버(gomoku)용 티켓인지 확인
    string transferGameType = root.TryGetProperty("gameType", out JsonElement gt)
        ? gt.GetString() ?? string.Empty
        : string.Empty;

    if (!string.Equals(transferGameType, "gomoku", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"[Gomoku] 잘못된 gameType: {transferGameType} (User_{playerId})");
        await session.SendAsync(BuildResponsePacket(new ProtoPacket
        {
            SLogin = new SLogin { ResultCode = LoginResultCode.LoginNotInQueue, PlayerId = 0 },
        }));
        session.Disconnect();
        return;
    }
}
```

### P1-D. MatchNotificationService ProcessMatchFoundAsync 분리

파일: `PlatformA.Game.Lobby/Services/MatchNotificationService.cs`

```csharp
private void OnMatchFound(RedisChannel channel, RedisValue message)
{
    _ = ProcessMatchFoundAsync(message);
}

internal async Task ProcessMatchFoundAsync(RedisValue message)
{
    try
    {
        using JsonDocument doc = JsonDocument.Parse(message.ToString());
        JsonElement root = doc.RootElement;

        int userId = root.GetProperty("userId").GetInt32();
        string host = root.GetProperty("host").GetString() ?? string.Empty;
        int port = root.GetProperty("port").GetInt32();
        string roomId = root.GetProperty("roomId").GetString() ?? string.Empty;
        string gameType = root.TryGetProperty("gameType", out JsonElement gt)
            ? gt.GetString() ?? string.Empty
            : string.Empty;

        _logger.LogInformation(
            "[MatchNotification] 매칭 성사 알림 → User_{UserId} room={RoomId}",
            userId, roomId);

        await _hubContext.Clients.User(userId.ToString()).SendAsync("MatchFound", new
        {
            host,
            port,
            roomId,
            gameType,
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "[MatchNotification] 메시지 처리 오류");
    }
}
```

### P1-E. MatchHistory Draw 결과 수정

파일: `PlatformA.Matching.API/Services/GameMatchService.cs`

```csharp
// 수정 전
Result = m.WinnerId == null ? "미완료"
             : m.WinnerId == userId ? "승리" : "패배",

// 수정 후
Result = m.Status != MatchStatus.Completed ? "미완료"
             : m.WinnerId == null ? "무승부"
             : m.WinnerId == userId ? "승리" : "패배",
```

### P1-F. DummyClient — TwoPlayerGomokuScenario (시나리오 9)

파일: `PlatformA.Game.DummyClient/Scenarios/TwoPlayerGomokuScenario.cs` (신규)

**흐름 (2개 Task를 병렬로 실행, 공유 채널로 조율):**

```
[Player 1 Task]                          [Player 2 Task]
1. Auth.API 로그인                       1. Auth.API 로그인
2. Lobby SignalR 연결                    2. Lobby SignalR 연결
3. MatchFound 핸들러 등록                3. MatchFound 핸들러 등록
4. RequestMatch("gomoku") 호출           4. RequestMatch("gomoku") 호출 (100ms 후)
5. MatchFound 수신 대기 (host/port/roomId/gameType)
6. Gomoku TCP 접속                       6. Gomoku TCP 접속
7. CLogin 전송 (JWT)                     7. CLogin 전송 (JWT)
8. SLogin 수신 확인                      8. SLogin 수신 확인
9. SGameStart 수신 확인
          ↓ 양쪽 SGameStart 수신 완료 후 게임 진행
10. CPlaceStone 교대 자동 진행 (15x15 중앙부터 나선형)
11. SBoardUpdate 수신 확인
12. SGameOver 수신 (승리 또는 무승부)
13. MatchRecord 결과 확인 (GET /api/gamematch/history)
14. 결과 출력 및 성공/실패 판정
```

**구현 세부 사항:**

- 두 플레이어 Task는 `Task.WhenAll`로 병렬 실행
- MatchFound payload 공유: `TaskCompletionSource<MatchFoundPayload>` 각 플레이어별
- Gomoku TCP 수신 루프: SLogin, SGameStart, SBoardUpdate, SGameOver 핸들링
- CPlaceStone 자동 전략: SGameStart의 `FirstTurnPlayerId` 기준으로 교대, 좌표는 (7,7)부터 순차 증가
- SGameOver 수신 시 → `lobbyUrl` 출력
- 타임아웃 방어: 각 대기 구간에 최대 30초 제한

**Program.cs 메뉴 추가:**

```
 9. [시나리오 9] 두 명 자동 매칭 → Gomoku 게임 완주 E2E 검증
```

---

## 영향 범위

| 파일 | 변경 유형 |
|------|---------|
| `PlatformA.Matching.API/Services/GameMatchService.cs` | publish try/catch, 구 루프 제거, Draw 결과 수정 |
| `PlatformA.Game.Gomoku/Packet/GomokuPacketHandler.cs` | gameType 검증 추가 |
| `PlatformA.Game.Lobby/Services/MatchNotificationService.cs` | ProcessMatchFoundAsync 분리 |
| `PlatformA.Game.DummyClient/Scenarios/TwoPlayerGomokuScenario.cs` | 신규 생성 |
| `PlatformA.Game.DummyClient/Program.cs` | 시나리오 9 메뉴 추가 |

테스트 추가 대상:
- `PlatformA.Tests.Matching.API` — `GameMatchService` publish 실패 로그 확인, Draw 결과 표시 수정
- `PlatformA.Tests.Game.Lobby` (없으면 신규) — `ProcessMatchFoundAsync` 정상/누락payload/잘못된JSON 케이스

---

## 제약 및 주의사항

- `AddPlayerToQueueAsync` 등 구 큐 관련 public 메서드는 외부 호출 여부 확인 후 삭제 판단
- `ProcessQueueAsync`, `ProcessMatchingAsync`는 private이므로 컴파일 오류 없이 제거 가능
- `BrokenCircuitException` 처리는 구 `ExecuteAsync`에 있었으므로 제거 시 try/catch에서 해당 분기도 제거
- TwoPlayerGomokuScenario는 로컬 실행 전제 — 모든 서비스가 기동된 상태에서 실행

## 구현 순서

1. GameMatchService — P0-A(publish try/catch) + P0-B(구 루프 제거) + P1-E(Draw 수정)
2. GomokuPacketHandler — P0-C(gameType 검증)
3. MatchNotificationService — P1-D(ProcessMatchFoundAsync 분리)
4. TwoPlayerGomokuScenario + Program.cs 메뉴 추가
5. 테스트 추가
6. 빌드 + 테스트 검증
