# 요구사항 명세: FixSessionDisconnectSafety

작성일: 2026-07-14
브랜치: 2026-07-14_FixSessionDisconnectSafety
소스: sprint-090.md (Phase C — task JSON 없음)

## 요구사항 요약
`Session.Disconnect()`에서 `_socket.RemoteEndPoint` 접근 시 `SocketException(107) ENOTCONN`이 발생하면 `OnDisconnected()`가 호출되지 않는 버그를 수정한다.
`RemoteEndPoint` 취득을 try-catch로 선행 분리하고, `Shutdown`/`Close`도 별도 try-catch로 보호하여 어떤 상황에서도 `OnDisconnected()`가 반드시 실행되도록 보장한다.

## 상세 요구사항

1. **RemoteEndPoint 선행 캡처**
   - `_socket.RemoteEndPoint` 접근을 `OnDisconnected()` 호출 이전에 try-catch로 감싼다.
   - 접근 실패 시 폴백 엔드포인트(`new IPEndPoint(IPAddress.None, 0)`) 또는 null을 전달한다.
   - 예외 발생 여부와 무관하게 `OnDisconnected()` 호출을 보장한다.

2. **Shutdown/Close 보호**
   - `_socket.Shutdown(SocketShutdown.Both)` 및 `_socket.Close()`를 별도 try-catch로 감싼다.
   - 예외 발생 시 로그 없이 무시한다 (이미 닫힌 소켓).
   - `finally` 블록에서 `_socket = null` 처리로 이후 참조 NPE 방지.

3. **기존 이중해제 방지 유지**
   - `Interlocked.Exchange(ref _disconnected, 1)` 패턴은 변경하지 않는다.

4. **API 변경 없음**
   - `Disconnect()` 메서드 시그니처 불변.
   - `OnDisconnected(EndPoint endPoint)` 시그니처 불변.
   - 외부에서 보이는 동작은 동일 (연결 종료 처리) — 내부 예외 처리만 강화.

## 영향 범위 (예상)

| 파일 | 변경 종류 | 이유 |
|------|---------|------|
| `PlatformA.Library/Network/Session.cs` | 수정 | Disconnect() 예외 처리 강화 |

테스트:
| 파일 | 변경 종류 | 이유 |
|------|---------|------|
| `PlatformA.Tests.Game.Gomoku/` 또는 새 테스트 | 추가 | 세션 종료 시 OnDisconnected 보장 테스트 |

## 제약 및 주의사항

- `OnDisconnected()`를 호출하기 전에 `_socket`을 null로 설정하면 안 된다 (`OnDisconnected` 내부에서 세션 정보 접근 가능).
- `_socket = null`은 `Shutdown`/`Close` 이후 `finally`에서만 수행한다.
- try-catch에서 잡는 예외 타입: `SocketException` (Socket.Connected 관련), `ObjectDisposedException` (이미 Dispose된 소켓 접근) 두 가지를 함께 처리한다.
- `OnDisconnected(endPoint)` 파라미터는 null-forgiving이 아니므로, 폴백 EndPoint 객체를 넘기되 하위 클래스가 null 방어 코드를 가지고 있어야 하는지 확인 (현재 `GameSession.OnDisconnected`는 `endPoint` 파라미터를 로그에만 사용 → null이어도 무방).

## 구현 접근 방향

```csharp
public void Disconnect()
{
    if (Interlocked.Exchange(ref _disconnected, 1) == 1)
        return;

    // 1. RemoteEndPoint를 먼저 캡처 — 실패해도 OnDisconnected는 반드시 호출
    EndPoint? endPoint = null;
    try { endPoint = _socket?.RemoteEndPoint; }
    catch (SocketException) { }
    catch (ObjectDisposedException) { }

    OnDisconnected(endPoint ?? new IPEndPoint(IPAddress.None, 0));

    // 2. Shutdown/Close — 이미 닫힌 경우 예외 무시
    try
    {
        _socket?.Shutdown(SocketShutdown.Both);
        _socket?.Close();
    }
    catch (SocketException) { }
    catch (ObjectDisposedException) { }
    finally
    {
        _socket = null;
    }
}
```

## 검증 기준

- [ ] `dotnet build PlatformA.sln` 오류 없음
- [ ] `dotnet test PlatformA.sln` 전체 통과
- [ ] `RemoteEndPoint` 접근이 실패하더라도 `OnDisconnected()`가 호출됨을 검증하는 테스트 존재
- [ ] `Interlocked` 이중 해제 방지 동작 유지
- [ ] `GameSession.OnDisconnected()` → `ReleaseLockAsync()` 및 `room.Leave()` 정상 실행 (기존 테스트로 커버)
