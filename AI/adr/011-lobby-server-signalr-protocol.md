# ADR-011: Game.Lobby 통신 프로토콜 — SignalR(WebSocket) 채택

## 상태: 확정

## 날짜: 2026-06-23

---

## 배경

Sprint #70 이전까지 `PlatformA.Game.Lobby`는 `PlatformA.Game.Server`를 리네임한 프로젝트로,
TCP + Protobuf 바이너리 프로토콜을 그대로 사용하고 있었다.

설계 의도는 모든 클라이언트의 첫 진입점이 되는 로비 서버로,
**상시 연결 유지(StateFull)**, **세션 관리**, **매칭 신청** 등의 역할을 수행해야 한다.
이러한 역할에는 TCP 소켓 직접 관리보다 ASP.NET Core SignalR이 적합하다.

---

## 결정

**Game.Lobby 서버는 ASP.NET Core + SignalR(WebSocket) 기반으로 구현한다.**

### 서버별 통신 프로토콜 명확화

| 서버 | 프로토콜 | 직렬화 | 역할 |
|------|---------|--------|------|
| **Game.Lobby** | SignalR (WebSocket) | JSON (SignalR 기본) | 로비, 매칭 신청, 유저 프레젠스 |
| **Game.Gomoku** | TCP (raw socket) | Protobuf (ADR-007) | 순수 게임 로직, 저지연 전투 |
| 향후 Game.CartRacer 등 | TCP | Protobuf | 동일 — 순수 게임 로직 |

- **ADR-002, ADR-007** (Binary/Protobuf 패킷)은 순수 게임 서버(Game.Gomoku 이후)에만 적용된다.
  Game.Lobby는 이 ADR들의 적용 범위에서 제외된다.

### SignalR 선택 이유

- **로비 특성에 적합**: 유저 입장/퇴장, 매칭 대기, 상태 브로드캐스트 → 양방향 이벤트 중심
- **플랫폼 이미 보유**: `Matching.API`가 이미 SignalR Hub을 사용 중 (신규 기술 도입 아님)
- **인증 통합 용이**: ASP.NET Core JWT 미들웨어와 자연스럽게 통합
- **개발 생산성**: TCP 소켓 직접 관리(버퍼, 파싱, 핸드셰이크) 불필요

### 클라이언트 연결 플로우

```
Client → Auth.API          (HTTP)     : JWT 발급
Client → Game.Lobby        (SignalR)  : 로비 입장 (OnConnectedAsync JWT 검증)
Client → Game.Lobby Hub    (SignalR)  : RequestMatch("gomoku") 호출
                                         └─ Matching.API (HTTP) : 매칭 처리
                                         └─ Redis channel:match_found
                               ↓
Client ← Game.Lobby Hub    (SignalR)  : MatchFound { host, port, roomId }
Client → Game.Gomoku       (TCP)      : CLogin + game_transfer 키 검증
                                         └─ 게임 진행 (TCP + Protobuf)
                               ↓
Client ← Game.Gomoku       (TCP)      : SGameOver
Client → Game.Lobby        (SignalR)  : (기존 연결 재개 또는 재연결)
```

### 매칭 알림 경로

- Matching.API가 `TryMatchAsync` 완료 시 Redis `channel:match_found`에 publish
- Game.Lobby의 `MatchNotificationService`(BackgroundService)가 구독
- 구독된 이벤트를 `IHubContext<LobbyHub>`를 통해 해당 유저에게 SignalR push

---

## 결과

- Game.Lobby는 더 이상 TCP 리스너, `GameSession`, `PacketHandler`(Protobuf)를 포함하지 않는다.
- 클라이언트는 JWT를 QueryString(`?access_token=`) 또는 Authorization 헤더로 전달한다.
- `PlatformA.Game.Server` 프로젝트는 폐기한다 (sln 제거 + 디렉토리 삭제).

---

## 관련 ADR

- ADR-002 (폐기): Game Server Binary 패킷 → Game.Lobby에 해당 없음
- ADR-007 (확정, 범위 제한): Protobuf 패킷 → Game.Gomoku 등 순수 게임 서버만 해당
- ADR-006: 매칭 시스템 — 알림 경로 보완 (이 ADR과 연계)
