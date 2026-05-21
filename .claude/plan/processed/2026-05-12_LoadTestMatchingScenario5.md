# 플랜: 시나리오 5 — 1000명 매칭 시스템 부하 테스트

## Context

시나리오 4는 1000명 로그인 + 대기열 통과까지 측정한다.
시나리오 5는 그 이후 단계 — **매칭 등록 → MatchFound/MatchTimeout 수신 → 방 입장** — 을 추가해
매칭 시스템의 처리 능력과 지연 시간을 부하 조건에서 검증하는 것이 목표다.

---

## 전체 흐름 (사용자 1명 기준)

```
[1] Auth.API  POST /login           → AccessToken 획득
[2] Ticketing POST /queue/enter     → 대기열 진입
[3] Ticketing GET  /queue/status    → Active 될 때까지 폴링 (SignalR 우선)
[4] Matching  SignalR 연결           → MatchFound / MatchTimeout 핸들러 등록
[5] Matching  POST /RequestMatch    → 매칭 큐 ZADD
[6]           ← MatchFound 수신     → 방 번호 확인, 매칭 지연 시간 기록
[7] Game.Server CEnterRoom(roomId) → TCP 방 입장 확인
```

1000명 동시 실행 → 500쌍 매칭 기대.
홀수 생존자(로그인/큐 실패 등)는 120초 후 MatchTimeout.

---

## 신규 파일

### `PlatformA.Game.DummyClient/Scenarios/LoadTestMatchingScenario.cs`

```csharp
public class LoadTestMatchingScenario
{
    // ─── 카운터 (Interlocked) ───────────────────────────────────────
    static int _loginOk, _loginFail;
    static int _queueOk, _queueFail;
    static int _matchOk, _matchTimeout, _matchFail;
    static int _roomOk, _roomFail;

    // ─── 지연 시간 수집 ─────────────────────────────────────────────
    static readonly ConcurrentBag<long> _matchLatenciesMs = new();

    public static async Task RunAsync()  { ... }
}
```

#### 핵심 메서드: `SimulateUserAsync(int index)`

```csharp
private static async Task SimulateUserAsync(int index)
{
    using var http = new HttpClient();

    // [1] 로그인
    var session = await AuthHelper.LoginAsync(http, user, pass);
    if (session == null) { Interlocked.Increment(ref _loginFail); return; }
    Interlocked.Increment(ref _loginOk);
    AuthHelper.ApplyToken(http, session);

    // [2~3] 대기열 진입 + Active 대기
    // LoginWaitScenario_1의 WaitForActiveAsync() 동일 패턴 재사용
    bool activated = await WaitForActiveAsync(http, session);
    if (!activated) { Interlocked.Increment(ref _queueFail); return; }
    Interlocked.Increment(ref _queueOk);

    // [4] Matching SignalR 연결
    var tcs = new TaskCompletionSource<MatchSuccessEvent>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    bool timedOut = false;

    var hub = new HubConnectionBuilder()
        .WithUrl(Consts.MATCH_HUB_URL, o =>
            o.AccessTokenProvider = () => Task.FromResult<string?>(session.AccessToken))
        .Build();

    hub.On<MatchSuccessEvent>("MatchFound",  e  => tcs.TrySetResult(e));
    hub.On<object>("MatchTimeout",           _  => { timedOut = true; tcs.TrySetCanceled(); });
    await hub.StartAsync();

    // [5] 매칭 요청
    var sw = Stopwatch.StartNew();
    await http.PostAsync(Consts.MATCH_API_URL, null);

    // [6] MatchFound 대기 (130s timeout — MATCH_TIMEOUT_SECONDS=120 + 여유)
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(130));
        var matchEvent = await tcs.Task.WaitAsync(cts.Token);
        sw.Stop();
        _matchLatenciesMs.Add(sw.ElapsedMilliseconds);
        Interlocked.Increment(ref _matchOk);

        // [7] TCP 방 입장
        await EnterRoomAsync(session.AccessToken, matchEvent.RoomId);
    }
    catch (OperationCanceledException) when (timedOut)
    {
        Interlocked.Increment(ref _matchTimeout);
    }
    catch
    {
        Interlocked.Increment(ref _matchFail);
    }
    finally
    {
        await hub.DisposeAsync();
    }
}
```

#### `WaitForActiveAsync` 재사용 방침

LoginWaitScenario_1에 있는 대기열 폴링 로직을 **그대로 복사하지 않고**
공통 헬퍼 메서드로 `AuthHelper.cs` 또는 신규 `QueueHelper.cs`에 추출 후 양쪽에서 참조한다.

추출 대상:
- `WaitForActiveAsync(HttpClient, TokenSession)` → `bool`
- 내부적으로 SignalR `QueueActivated` 우선, 10초 fallback 폴링

#### 최종 리포트 출력 항목

```
=== 시나리오 5 결과 ===
[Auth]    성공: 980  실패: 20
[Queue]   성공: 970  실패: 10
[Matching]
  성공(MatchFound):  960  ( 99.0% )
  타임아웃:            8
  실패:               2
  처리량:           4.8 쌍/초
[Latency] Avg: 412ms  P50: 380ms  P95: 820ms  P99: 1200ms
[Room]    입장 성공: 1920명  실패: 0명
===========================
```

---

## 수정 파일

### `Program.cs` case "5"

```csharp
case "5":
    await LoadTestMatchingScenario.RunAsync();
    break;
```

### `AuthHelper.cs` (또는 신규 `QueueHelper.cs`)

`WaitForActiveAsync` 추출 후 LoginWaitScenario_1에서도 호출하도록 변경.

---

## 변경 파일 요약

| 파일 | 변경 |
|------|------|
| `Scenarios/LoadTestMatchingScenario.cs` | 신규 생성 |
| `Scenarios/AuthHelper.cs` | `WaitForActiveAsync` 추출 추가 |
| `Scenarios/LoginWaitScenario_1.cs` | 추출된 헬퍼 호출로 교체 |
| `Program.cs` | case "5" 연결 |

---

## 검증 절차

```bash
# 빌드 확인
cd PlatformA && dotnet build PlatformA.sln -q

# 실행 환경 (모두 기동 필요)
# - Auth.API
# - Ticketing.API
# - Matching.API
# - Game.Server
# - Redis Cluster

# 실행
dotnet run --project PlatformA.Game.DummyClient
# → 5 선택
```

예상 결과:
- 1000명 중 로그인 성공자들이 매칭 완료
- P95 매칭 지연 < 1초 (워커 200ms 폴링 기준 이론값)
- MatchTimeout 발생 건수 = 로그인/큐 실패로 인한 홀수 생존자 수

---

## 구현 우선순위

1. `WaitForActiveAsync` 헬퍼 추출 (기반 작업)
2. `LoadTestMatchingScenario.cs` 핵심 로직
3. 리포트 + 지연 시간 통계 (P50/P95/P99)
4. `Program.cs` 연결
