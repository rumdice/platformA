# Lecture 4 — Action / delegate / Func / 이벤트 핸들러 콜백

> 예시 코드는 모두 이 프로젝트(PlatformA + Game.Gomoku)에서 가져왔다.

---

## 1. 핵심 결론

이 키워드들은 한 문장으로 요약된다.

```
메서드를 변수처럼 저장하고, 나중에 또는 다른 곳에서 실행하는 방법이다.
```

보통 코드는 데이터를 변수에 담는다.

```csharp
int userId  = 42;
string gameType = "gomoku";
```

delegate / Action / Func 은 **메서드 자체**를 변수에 담는다.

```csharp
// GameRoom 안에 Action 큐가 있다
Queue<Action> _jobQueue = new Queue<Action>();

// 나중에 실행할 "돌 놓기" 로직을 큐에 집어넣는다
Action job = () => room.HandlePlaceStone(session, x, y);
_jobQueue.Enqueue(job);

// 꺼내서 실행
job.Invoke();
```

이것이 가능해지면 코드를 **나중에(큐에 쌓아두고)** 실행하거나,
**바깥에서 주입**하거나, **조건에 따라 교체**할 수 있다.

---

## 2. delegate — 패킷 핸들러의 타입을 직접 정의한다

`delegate`는 메서드의 생김새(파라미터, 반환 타입)를 하나의 타입으로 선언하는 키워드다.

`PacketManager.cs`에서 가장 직접적인 형태로 쓰인다.

```csharp
// PacketManager.cs

// "이런 형태의 메서드를 담을 수 있는 타입"을 선언한다
public delegate void PacketHandlerDelegate<T>(T session, Packet packet)
    where T : Session;

// 선언한 타입으로 딕셔너리를 만든다 — 패킷 종류(ID)와 처리 메서드를 매핑한다
private Dictionary<Packet.PayloadOneofCase, PacketHandlerDelegate<T>> _handlers
    = new Dictionary<Packet.PayloadOneofCase, PacketHandlerDelegate<T>>();
```

메서드를 값처럼 꺼내 딕셔너리에 저장하고, 패킷이 도착하면 꺼내 실행한다.

```csharp
// 등록 — [PacketHandler] 어트리뷰트가 붙은 메서드를 리플렉션으로 찾아 딕셔너리에 넣는다
PacketHandlerDelegate<T> handler =
    (PacketHandlerDelegate<T>)Delegate.CreateDelegate(
        typeof(PacketHandlerDelegate<T>), method);

_handlers.Add(attribute.OneofCase, handler);
// [PacketManager] 라우팅 등록 완료: CPlaceStone -> Handle_C_PlaceStone()

// 실행 — 패킷이 도착하면 ID로 꺼내 호출한다
public void HandlePacket(T session, Packet packet)
{
    if (_handlers.TryGetValue(packet.PayloadCase, out PacketHandlerDelegate<T> handler))
        handler.Invoke(session, packet);  // 저장해둔 메서드를 실행
}
```

```
패킷 종류가 CLogin      이면 → Handle_C_Login()  실행
패킷 종류가 CPlaceStone 이면 → Handle_C_PlaceStone() 실행
```

if/switch 없이 새 패킷 처리를 `[PacketHandler]` 어트리뷰트만 붙여 추가할 수 있다.

---

## 3. Action — 반환값 없는 메서드를 큐에 담는다

`Action`은 `void`를 반환하는 메서드를 담을 수 있는 미리 정의된 타입이다.

```
Action            → void를 반환하는 메서드
Action<T>         → T 파라미터 하나를 받고 void를 반환하는 메서드
```

### 3-1. JobQueue — Action을 줄 세워 순차 실행한다

`JobQueue.cs`가 이 패턴의 교과서다.

```csharp
// JobQueue.cs

// Action 타입의 큐 — 실행할 메서드들이 줄을 선다
private Queue<Action> _jobQueue = new Queue<Action>();

// 외부에서 실행할 메서드를 큐에 넣는다
public void Push(Action job)
{
    lock (_lock)
    {
        _jobQueue.Enqueue(job);   // 메서드를 저장

        if (!_isExecuting)
        {
            isFirst = true;
            _isExecuting = true;
        }
    }
    if (isFirst) Flush();
}

// 쌓인 일거리를 차례로 꺼내 실행한다
private void Flush()
{
    while (true)
    {
        Action? action = null;
        lock (_lock)
        {
            if (_jobQueue.Count == 0) { _isExecuting = false; return; }
            action = _jobQueue.Dequeue();
        }
        action.Invoke();  // 저장해뒀던 메서드를 실행
    }
}
```

`GameRoom.cs`가 이 JobQueue를 감싸서 공개한다.

```csharp
// GameRoom.cs

public void Push(Action job)
{
    _jobQueue.Push(job);
}
```

### 3-2. room.Push(() => ...) — 멀티스레드 안에서 안전하게 게임 상태를 바꾼다

패킷 핸들러(`GomokuPacketHandler.cs`)가 `room.Push()`를 쓰는 방식이다.

```csharp
// GomokuPacketHandler.cs

[PacketHandler(ProtoPacket.PayloadOneofCase.CPlaceStone)]
public static void Handle_C_PlaceStone(GomokuSession session, ProtoPacket packet)
{
    CPlaceStone req = packet.CPlaceStone;
    if (session.Room is not GomokuRoom room)
        return;

    // "돌 놓기"를 지금 실행하지 않는다
    // 람다(Action)로 감싸서 큐에 넣는다 — 나중에 단일 스레드로 실행됨
    room.Push(() => room.HandlePlaceStone(session, req.X, req.Y));
}
```

로그인 처리도 같은 패턴이다.

```csharp
room.Push(() =>
{
    room.Enter(session);                    // 방 입장
    _ = session.SendAsync(BuildResponsePacket(new ProtoPacket
    {
        SLogin = new SLogin
        {
            ResultCode = LoginResultCode.LoginSuccess,
            PlayerId = playerId
        },
    }));                                    // 성공 응답 전송
});
```

```
Push()를 호출한 스레드는 여럿이어도 괜찮다.
실제로 room.Enter()와 room.HandlePlaceStone()은
항상 한 번에 하나씩, 순서대로 실행된다.
→ lock 없이 레이스 컨디션 차단.
```

### 3-3. 턴 타임아웃 감시 — 별도 스레드에서 Push()로 안전하게 개입한다

`GomokuRoom.cs`의 타임아웃 루프가 이 패턴을 잘 보여준다.

```csharp
// GomokuRoom.cs

_ = Task.Run(async () =>
{
    while (GameState == GomokuGameState.InProgress)
    {
        await Task.Delay(1000);  // 1초 대기

        // 별도 스레드이지만 Push()로 게임 상태를 안전하게 조작한다
        Push(() =>
        {
            if (GameState != GomokuGameState.InProgress || _turn == null)
                return;
            if (_turn.IsTimeout())
            {
                int winner = _turn.GetOpponentId(_turn.CurrentTurnPlayerId);
                FinishGame(winner, GameOverReason.Timeout);
            }
        });
    }
});
```

타임아웃 감시는 별도 스레드에서 돌지만, 실제 `FinishGame()` 호출은 `Push()` 안에서 이루어진다. 결과적으로 게임 상태는 항상 단일 스레드에서만 변경된다.

---

## 4. Func — 반환값 있는 메서드를 변수에 담는다

`Action`과 동일한 개념이지만 마지막 타입 파라미터가 반환 타입이다.

```
Func<TResult>         → TResult를 반환하는 메서드
Func<T, TResult>      → T를 받아서 TResult를 반환하는 메서드
Func<T, Task<TResult>>→ T를 받아서 비동기로 TResult를 반환하는 메서드
```

### 4-1. RedisManager.ExecuteAsync — Redis 연결 관리를 캡슐화한다

`GameMatchService.cs`에서 가장 많이 반복되는 구조다.

```csharp
// GameMatchService.cs

// Func<IDatabase, Task<string?>> 을 람다로 전달
string? val = await _redisManager.ExecuteAsync(
    db => db.StringGetAsync(ratingKey));

// Func<IDatabase, Task<bool>> 을 람다로 전달
bool removed = await _redisManager.ExecuteAsync(
    db => db.SortedSetRemoveAsync(queueKey, userId));

// Func<IDatabase, Task<bool>> + TTL 포함
await _redisManager.ExecuteAsync(db =>
    db.StringSetAsync(ratingKey, rating.ToString(), TimeSpan.FromHours(1)));
```

`ExecuteAsync` 내부에서 Redis 연결을 열고, Polly 재시도 파이프라인을 적용하고, 전달받은 람다를 실행한다. 호출부는 Redis 연결 관리를 신경 쓰지 않는다.

```
ExecuteAsync가 담당하는 것 : Redis 연결 열기, 재시도, 회로차단기
호출부가 담당하는 것     : "무엇을 조회/저장할지"만 람다로 전달
```

`GomokuPacketHandler.cs`에서도 같은 패턴이 나온다.

```csharp
// GomokuPacketHandler.cs

// Func<IDatabase, Task<string?>>
string? transferJson = await RedisManager.Instance.ExecuteAsync(
    db => db.StringGetAsync(transferKey));

// Func<IDatabase, Task<bool>>
await RedisManager.Instance.ExecuteAsync(
    db => db.KeyDeleteAsync(transferKey));  // 티켓 소비 (한 번만 사용 가능)
```

### 4-2. LINQ 체인 — Func이 연속으로 쌓인다

`GameMatchService.cs`의 이력 조회가 대표적이다.

```csharp
// GameMatchService.cs

return await db.MatchRecords
    .Where(m => m.Player1Id == userId || m.Player2Id == userId)  // Func<MatchRecord, bool>
    .OrderByDescending(m => m.CreatedAt)                          // Func<MatchRecord, DateTime>
    .Take(limit)
    .Select(m => new MatchHistoryDto                              // Func<MatchRecord, MatchHistoryDto>
    {
        MatchId    = m.Id,
        OpponentId = m.Player1Id == userId ? m.Player2Id : m.Player1Id,
        Result     = m.Status != MatchStatus.Completed ? "미완료"
                         : m.WinnerId == null         ? "무승부"
                         : m.WinnerId == userId       ? "승리" : "패배",
        MatchedAt  = m.CreatedAt,
    })
    .ToListAsync();
```

`Where`, `OrderByDescending`, `Select`는 내부적으로 전부 `Func` 파라미터를 받는다. 각 단계에서 "어떤 조건으로 필터할지", "어떤 필드로 정렬할지", "어떤 형태로 변환할지"를 람다로 주입한다.

---

## 5. Channel — 고속 Action 큐의 비동기 버전 (EngineService)

주식 매칭 엔진(`EngineService.cs`)은 `Action` 대신 `Channel<Order>`를 사용한다.
개념은 `JobQueue`와 같되, 비동기 I/O에 최적화되어 있다.

```csharp
// EngineService.cs

// 고속 비동기 큐 — 주문(Order)이 들어오면 담아두고 소비자가 꺼낸다
private readonly Channel<Order> _orderChannel = Channel.CreateUnbounded<Order>(
    new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
```

```
JobQueue (Action 기반)       → 게임 방 로직 (CPlaceStone, Enter, Leave)
Channel<Order> (Order 기반)  → 주식 주문 매칭 (Buy/Sell 체결)
둘 다 "하나씩 순서대로" 처리하여 lock 없이 상태를 보호한다.
```

**Producer** — 컨트롤러가 주문을 넣는다.

```csharp
public async ValueTask EnqueueOrderAsync(Order order)
{
    await _orderChannel.Writer.WriteAsync(order);  // 큐에 쏙 집어넣고 바로 리턴
}
```

**Consumer** — 백그라운드 워커가 하나씩 꺼내 처리한다.

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    await foreach (var order in _orderChannel.Reader.ReadAllAsync(stoppingToken))
    {
        _orderBook.ProcessOrder(order);  // 이 블록은 절대 동시 실행되지 않는다

        var snapshot = _orderBook.GetSnapshot();
        await _hubContext.Clients.All.SendAsync("ReceiveOrderBook", snapshot);
        await _hubContext.Clients.All.SendAsync("ReceiveLog", $"주문(ID:{order.Id}) 처리됨");
    }
}
```

---

## 6. BackgroundService.ExecuteAsync — 프레임워크가 콜백을 호출한다

`GameMatchService.cs`와 `EngineService.cs`는 둘 다 `BackgroundService`를 상속한다.

```csharp
// GameMatchService.cs
public class GameMatchService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int tickCount = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            tickCount++;
            if (tickCount >= 1500)  // 5분(1500 × 200ms)마다
            {
                tickCount = 0;
                _ = AbandonStaleMatchesAsync();  // stale Pending 레코드 정리
            }
            await Task.Delay(200, stoppingToken);
        }
    }
}
```

`ExecuteAsync`를 **override(재정의)** 하는 것은 "무엇을 반복 실행할지"를 프레임워크에 주입하는 행위다. ASP.NET Core가 애플리케이션 시작 시 이 메서드를 호출한다. **언제 시작하고 언제 멈출지**는 프레임워크가 결정하고, **무엇을 할지**는 이 클래스가 결정한다.

`CancellationToken`이 그 신호를 전달하는 수단이다.

---

## 7. Fire-and-Forget — 결과를 버리는 패턴

반환값 없는 `Action`처럼 결과를 버리고 실행만 촉발하는 경우가 여럿 있다.

**`_ = Task.Run(...)` — 타임아웃 감시 루프를 백그라운드에서 실행**

```csharp
// GomokuRoom.cs

_ = Task.Run(async () =>
{
    while (GameState == GomokuGameState.InProgress)
    {
        await Task.Delay(1000);
        Push(() => { /* 타임아웃 체크 */ });
    }
});
```

**`_ = ReportMatchResultAsync(...)` — 결과 보고 실패해도 게임 흐름 방해 없음**

```csharp
// GomokuRoom.cs

private void FinishGame(int winnerId, GameOverReason reason)
{
    GameState = GomokuGameState.Finished;
    Broadcast(/* SGameOver 패킷 */);

    _ = ReportMatchResultAsync(winnerId, reason);  // 실패해도 게임 흐름은 계속
    GomokuRoomManager.Instance.Remove(_roomId);
}
```

**`_ = AbandonStaleMatchesAsync()` — 정리 작업이 200ms 루프를 막지 않음**

```csharp
// GameMatchService.cs

if (tickCount >= 1500)
{
    tickCount = 0;
    _ = AbandonStaleMatchesAsync();  // Task를 명시적으로 버림
}
```

공통점: 결과를 기다리지 않는다. `Action`이 `void`를 반환하는 것처럼, 반환된 `Task`를 `_`로 무시한다.

---

## 8. Lua 스크립트 — 실행할 코드를 문자열로 저장한다

`GameMatchService.cs`에만 있는 독특한 패턴이다.

```csharp
// GameMatchService.cs

// Lua 코드를 const string에 담아두고 — 나중에 Redis 서버에서 실행한다
private const string MATCH_STRICT_SCRIPT = @"
local candidates = redis.call('ZRANGEBYSCORE', KEYS[1], ARGV[2], ARGV[3])
for i = 1, #candidates do
    if candidates[i] ~= ARGV[1] then
        redis.call('ZREM', KEYS[1], candidates[i])
        return {candidates[i]}
    end
end
return {}";
```

호출부는 람다 패턴과 형태가 거의 같다.

```csharp
var rawResult = await _redisManager.ExecuteAsync(db =>
    db.ScriptEvaluateAsync(
        MATCH_STRICT_SCRIPT,               // 실행할 코드 (Lua)
        new RedisKey[] { queueKey },       // KEYS[1] — 매칭 큐
        new RedisValue[] { userId, minScore, maxScore }));  // ARGV[1~3]
```

```
C# 람다 = C# 코드를 변수에 담아, 나중에 이 프로세스에서 실행
Lua 스크립트 = Lua 코드를 문자열에 담아, 나중에 Redis 서버에서 실행
"실행할 코드를 값처럼 저장한다"는 개념은 동일하다.
```

---

## 9. Action vs Func vs delegate 비교

| 타입 | 반환값 | 이 프로젝트 사용 위치 |
|------|--------|---------------------|
| `delegate` | 직접 정의 | `PacketHandlerDelegate<T>` — 패킷 ID → 처리 메서드 매핑 |
| `Action` | 없음 (void) | `JobQueue._jobQueue` — 게임 방 로직 큐 |
| `Action` | 없음 (void) | `room.Push(() => ...)` — 오목 패킷 핸들러 |
| `Func<IDatabase, Task<T>>` | Task\<T\> | `_redisManager.ExecuteAsync(db => ...)` — Redis 쿼리 주입 |
| `Func<MatchRecord, bool>` | bool | `.Where(m => ...)` — LINQ 필터 조건 |
| `Func<MatchRecord, MatchHistoryDto>` | DTO | `.Select(m => new ...)` — LINQ 변환 규칙 |

```
거의 대부분의 경우 Action 이나 Func 으로 해결된다.
delegate 를 직접 선언하는 경우는 PacketHandlerDelegate<T>처럼
제네릭 타입 제약이 필요하거나
Action / Func 으로 표현할 수 없는 형태일 때다.
```

---

## 10. 람다식 — 이름 없는 메서드를 인라인으로 작성한다

`Action`, `Func`에 메서드를 전달할 때 별도 메서드를 만들지 않고 그 자리에서 쓰는 문법이다.

```csharp
// 메서드를 따로 정의하는 방식
room.Push(PlaceStoneJob);
void PlaceStoneJob() { room.HandlePlaceStone(session, x, y); }

// 람다로 인라인으로 쓰는 방식
room.Push(() => room.HandlePlaceStone(session, x, y));
```

한 줄이면 중괄호를 생략할 수 있다.

```csharp
// GameRoom.cs
public OrderBook GetOrderBook() => _orderBook;  // 표현식 본문 메서드

// GomokuRoom.cs
internal static void SetLogger(ILogger logger) => _logger = logger;
```

여러 줄이면 중괄호로 감싼다.

```csharp
room.Push(() =>
{
    room.Enter(session);
    _ = session.SendAsync(BuildResponsePacket(/* SLogin 성공 */));
});
```

---

## 11. 최종 요약 — 이 프로젝트의 흐름으로

```
클라이언트가 CPlaceStone 패킷을 보낸다
    ↓
PacketManager.HandlePacket(session, packet)
    → _handlers[CPlaceStone].Invoke()           ← delegate
    ↓
Handle_C_PlaceStone(session, packet)
    → room.Push(() => room.HandlePlaceStone(x, y))  ← Action 람다
    ↓
JobQueue.Flush()
    → action.Invoke()                            ← Action 실행
    ↓
room.HandlePlaceStone(session, x, y)
    → WinChecker.CheckWin(...)
    → FinishGame(winner, Reason.FiveInRow)
    → _ = ReportMatchResultAsync(...)            ← Fire-and-Forget
    → _redisManager.ExecuteAsync(db => ...)      ← Func<IDatabase, Task>
```

```
가장 중요한 한 줄:

메서드를 미리 정해두지 않고, 실행 시점에 외부에서 주입받는 것이
delegate / Action / Func / 람다의 공통 목적이다.
```
