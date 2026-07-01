# 요구사항 명세: CompleteLibraryGameAbstraction

작성일: 2026-07-01
브랜치: 2026-07-01_CompleteLibraryGameAbstraction
소스: 사용자 설명 (task JSON summary)

## 요구사항 요약
PlatformA.Library.Game에 게임 서버 공통 인터페이스(IGameSession, IGameRoom)와
추상 기반 클래스(GameRoomManagerBase<TRoom, TKey>)를 추가하고,
GameRoom.Enter()를 virtual로 승격하여 GomokuRoom이 `new` 대신 `override`를 사용하도록 개선한다.
GomokuRoomManager는 GameRoomManagerBase<GomokuRoom, string>을 상속하도록 리팩토링한다.

## 현재 상태 분석

### Library.Game 현황
- `GameSession` (abstract) — Session 상속, OnConnected/OnDisconnected 구현, OnRecv 추상
- `GameRoom` (concrete) — Push/Enter/Leave/Broadcast 제공, `Enter(GameSession)` non-virtual
- `GameRoomManager` (concrete singleton) — int 키로 방 관리, Gomoku에서 미사용

### Gomoku 현황
- `GomokuSession` — GameSession 상속, OnRecv 구현 ✅
- `GomokuRoom` — GameRoom 상속, `new Enter(GomokuSession)` 로 숨김 (코드 스멜)
- `GomokuRoomManager` — GameRoomManager와 무관한 별도 singleton, string 키로 방 관리

## 상세 요구사항

### 1. IGameSession 인터페이스 추가
파일: `PlatformA.Library.Game/Network/IGameSession.cs`

```csharp
public interface IGameSession
{
    int SessionId { get; set; }
    string? LoginLockValue { get; set; }
    Task SendAsync(byte[] data);
    void Disconnect();
}
```

### 2. IGameRoom 인터페이스 추가
파일: `PlatformA.Library.Game/Core/IGameRoom.cs`

```csharp
public interface IGameRoom
{
    int RoomId { get; set; }
    void Push(Action job);
    void Enter(GameSession session);
    void Leave(GameSession session);
    void Broadcast(byte[] packet);
    IReadOnlyList<GameSession> Sessions { get; }
}
```

### 3. GameRoom.Enter() virtual 승격
파일: `PlatformA.Library.Game/Core/GameRoom.cs`

```csharp
// Before:
public void Enter(GameSession session)

// After:
public virtual void Enter(GameSession session)
```

`Leave()`, `Broadcast()`도 `virtual`로 선언하여 향후 확장 가능하게 한다.
GameRoom이 IGameRoom 인터페이스를 구현하도록 선언을 추가한다.

### 4. GameRoomManagerBase<TRoom, TKey> 추상 기반 클래스 추가
파일: `PlatformA.Library.Game/Core/GameRoomManagerBase.cs`

```csharp
public abstract class GameRoomManagerBase<TRoom, TKey>
    where TRoom : GameRoom
    where TKey : notnull
{
    protected readonly ConcurrentDictionary<TKey, TRoom> _rooms = new();

    public TRoom GetOrCreate(TKey key, Func<TKey, TRoom> factory)
    {
        return _rooms.GetOrAdd(key, factory);
    }

    public TRoom? Find(TKey key)
    {
        _rooms.TryGetValue(key, out TRoom? room);
        return room;
    }

    public bool Remove(TKey key)
    {
        return _rooms.TryRemove(key, out _);
    }

    public int Count => _rooms.Count;
}
```

### 5. GomokuRoom.Enter() override 전환
파일: `PlatformA.Game.Gomoku/Core/GomokuRoom.cs`

- `new void Enter(GomokuSession session)` 패턴 제거
- `public override void Enter(GameSession session)` 로 변경
  - session을 GomokuSession으로 캐스팅하여 기존 로직 유지

### 6. GomokuRoomManager를 GameRoomManagerBase 상속으로 리팩토링
파일: `PlatformA.Game.Gomoku/Core/GomokuRoomManager.cs`

```csharp
public class GomokuRoomManager : GameRoomManagerBase<GomokuRoom, string>
{
    public static GomokuRoomManager Instance { get; } = new GomokuRoomManager();
    private GomokuRoomManager() { }

    public GomokuRoom GetOrCreate(string roomId)
        => GetOrCreate(roomId, id => new GomokuRoom(id));
}
```

(Remove, Find 메서드는 기반 클래스에서 상속되므로 제거)

### 7. GameSession이 IGameSession 구현 선언
파일: `PlatformA.Library.Game/Network/GameSession.cs`
- `public abstract class GameSession : Session, IGameSession` 선언 추가

## 영향 범위 (예상)

| 파일 | 변경 유형 | 위험도 |
|------|---------|--------|
| `PlatformA.Library.Game/Network/IGameSession.cs` | 신규 | 🟢 LOW |
| `PlatformA.Library.Game/Core/IGameRoom.cs` | 신규 | 🟢 LOW |
| `PlatformA.Library.Game/Core/GameRoomManagerBase.cs` | 신규 | 🟢 LOW |
| `PlatformA.Library.Game/Core/GameRoom.cs` | 수정 (virtual 추가, IGameRoom 구현) | 🟢 LOW |
| `PlatformA.Library.Game/Network/GameSession.cs` | 수정 (IGameSession 구현 선언) | 🟢 LOW |
| `PlatformA.Game.Gomoku/Core/GomokuRoom.cs` | 수정 (Enter override 전환) | 🟡 MEDIUM |
| `PlatformA.Game.Gomoku/Core/GomokuRoomManager.cs` | 수정 (기반 클래스 상속) | 🟡 MEDIUM |

## 제약 및 주의사항
- `GameRoomManager`(int 키 singleton)는 현재 Gomoku에서 미사용이므로 변경 불필요
- GomokuRoom.Enter()의 `GomokuSession`을 `GameSession`으로 캐스팅할 때 null 체크 필요
- 기존 동작 변경 없이 순수 추상화 레이어 추가가 목표
- 모든 기존 테스트 통과 유지 필수 (현재 45개 Gomoku 테스트)

## 구현 접근 방향
1. `IGameSession`, `IGameRoom` 인터페이스 파일 먼저 작성
2. `GameRoomManagerBase<TRoom, TKey>` 추가
3. `GameRoom`에 `IGameRoom` 구현 및 virtual 선언 추가
4. `GameSession`에 `IGameSession` 구현 선언 추가
5. `GomokuRoom.Enter()` override 전환
6. `GomokuRoomManager` 기반 클래스 상속으로 단순화
7. 빌드 → 테스트 → 완료

## 검증 기준
- `dotnet build PlatformA.sln` 빌드 오류 0개
- `dotnet test PlatformA.sln` 224개 전체 통과 (특히 Gomoku 45개)
- `IGameRoom`, `IGameSession`, `GameRoomManagerBase` 클래스 존재 확인
- `GomokuRoom.Enter()` 에서 `override` 키워드 사용
- `GomokuRoomManager` 에서 `GameRoomManagerBase` 상속
