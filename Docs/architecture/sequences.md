# 시퀀스 다이어그램

## 1. 로그인 / 인증 플로우

```mermaid
sequenceDiagram
  participant C as 클라이언트
  participant A as Auth API
  participant DB as MariaDB
  participant R as Redis

  C->>+A: POST /api/Auth/login<br/>{username, password}
  A->>A: RedisRateLimit 체크 (10회/분)
  A->>+DB: SELECT * FROM players WHERE username=?
  DB-->>-A: 결과 반환

  alt 신규 유저
    A->>A: BCrypt.HashPassword(password)
    A->>DB: INSERT INTO players VALUES (...)
    Note over A,DB: 동시 가입 Race Condition → 1062 Duplicate Key 감지 후 재조회
  else 기존 유저
    A->>A: BCrypt.Verify(입력값, 저장된 해시)
  end

  A->>A: GenerateJwtToken(playerId) ← 15분 유효
  A->>A: GenerateRefreshToken(playerId) ← "playerId:uuid"
  A->>+R: SET refresh:{playerId} token EX 604800
  R-->>-A: OK
  A-->>-C: 200 {token, refreshToken, playerId}
```

**Token Rotation (POST /api/Auth/refresh):**

```mermaid
sequenceDiagram
  participant C as 클라이언트
  participant A as Auth API
  participant R as Redis

  C->>+A: POST /api/Auth/refresh<br/>{refreshToken}
  A->>A: refreshToken에서 playerId 파싱
  A->>+R: Lua: GET refresh:{playerId}<br/>→ 일치 확인 → DEL (원자적)
  R-->>-A: 이전 토큰 반환 (또는 null)

  alt 토큰 일치
    A->>A: 새 AccessToken + RefreshToken 생성
    A->>R: SET refresh:{playerId} newToken EX 604800
    A-->>C: 200 {token, refreshToken}
  else 불일치 / 만료
    A-->>-C: 401 Unauthorized
  end
```

---

## 2. 대기열 진입 → 입장권 발급

```mermaid
sequenceDiagram
  participant C as 클라이언트
  participant T as Ticketing API
  participant R as Redis

  C->>+T: POST /api/queue/enter (JWT)
  T->>T: JWT → playerId 추출
  T->>+R: Lua: ZCARD {ticket:queue}:global<br/>< 10,000이면 ZADD (원자적)
  R-->>-T: rank
  T-->>-C: 200 {rank, nextPollDelay}

  loop Smart Polling
    C->>+T: GET /api/queue/status (JWT)
    T->>+R: EXISTS ticket:active:user:{userId}
    R-->>-T: 0 or 1

    alt Active (입장권 발급됨)
      T-->>C: 200 {status: "Active"}
    else 대기 중
      T->>R: Lua: ZADD heartbeat + ZRANK queue (원자적)
      T-->>-C: 200 {rank, nextPollDelay}
    end
  end

  Note over T,R: 백그라운드 워커가 주기적으로<br/>대기열 앞 N명에게 입장권 발급<br/>(ticket:active:user:{userId} TTL 5분)
```

---

## 3. 매칭 요청 → Game.Gomoku 접속

> **현재 아키텍처**: 클라이언트는 Matching.API에 직접 접근하지 않습니다.
> 모든 매칭 흐름은 Game.Lobby SignalR 허브를 경유합니다.

```mermaid
sequenceDiagram
  participant C as 클라이언트
  participant L as Game.Lobby (SignalR :7777)
  participant M as Matching API (내부 HTTP :7002)
  participant R as Redis
  participant DB as MariaDB
  participant G as Game.Gomoku (TCP :7778)

  C->>+L: SignalR 연결 /hubs/lobby
  C->>L: RequestMatch("gomoku")
  L->>L: JWT → playerId 추출<br/>Context.Items["MatchGameType"] = "gomoku"

  L->>+M: POST /api/gamematch/request<br/>{userId, gameType} (내부 HTTP)
  M->>+R: ZADD queue:gamematch:gomoku score=ELO playerId
  R-->>-M: OK
  M-->>-L: 200 (매칭 대기 중)
  L-->>C: SignalR ACK

  Note over M,R: 백그라운드 워커 (200ms 주기)
  loop 매칭 처리
    M->>+R: Lua: ELO 범위 내 후보 조회 (±200 → ±400 → ±800)
    R-->>-M: [player1, player2] or []

    alt 2명 확보
      M->>R: INCR global:room_id → roomId
      M->>+DB: INSERT match_records (player1, player2, InProgress)
      DB-->>-M: OK
      M->>R: PUBLISH MATCH_FOUND_CHANNEL {roomId, [p1, p2], gameType}
    else 타임아웃 (2분 초과)
      M->>R: ZREM queue:gamematch:gomoku playerId
      M->>R: PUBLISH MATCH_FOUND_CHANNEL {timeout: true, userId}
    end
  end

  Note over L,R: MatchNotificationService (Game.Lobby 내 BackgroundService)
  L->>+R: SUB MATCH_FOUND_CHANNEL
  R-->>-L: 매칭 성사 이벤트 수신

  alt MatchFound
    L-->>C: SignalR "MatchFound"<br/>{roomId, gameServerIp, port: 7778}
  else MatchTimeout
    L-->>C: SignalR "MatchTimeout"
  end

  C->>+G: TCP Connect :7778
  C->>G: CLogin {roomId, jwtToken}
  G->>G: JWT 검증 → playerId 추출
  G->>+R: EXISTS ticket:active:user:{playerId}
  R-->>-G: 1 (입장권 확인됨)
  G->>R: DEL ticket:active:user:{playerId} (티켓 회수)
  G->>+R: SET player:login_lock:{playerId} guid NX EX 86400
  R-->>-G: OK (락 획득)
  G->>G: GomokuRoom.Enter(session)
  G-->>-C: SLogin {success: LoginSuccess, playerId}
```

**매칭 취소 (CancelMatch):**

```mermaid
sequenceDiagram
  participant C as 클라이언트
  participant L as Game.Lobby (SignalR)
  participant M as Matching API (내부 HTTP)
  participant R as Redis

  C->>+L: SignalR CancelMatch
  L->>L: Context.Items["MatchGameType"] 조회
  L->>+M: POST /api/gamematch/cancel<br/>{userId, gameType} (내부 HTTP)
  M->>+R: ZREM queue:gamematch:{gameType} userId
  R-->>-M: 1 (제거됨) or 0 (없음)
  M-->>-L: 200 or 404
  L-->>-C: SignalR ACK
```

---

## 4. Game Server 패킷 처리 플로우

```mermaid
flowchart TD
  A[TCP 패킷 수신] --> B["4바이트 크기 헤더 파싱\n(Little-Endian)"]
  B --> C["Protobuf Envelope 파싱\nPacket.Parser.ParseFrom(buffer)"]
  C --> D{PayloadOneofCase}

  D -->|CLogin| E[Handle_C_Login]
  D -->|CMove| F[Handle_C_Move]
  D -->|CEnterRoom| G[Handle_C_EnterRoom]
  D -->|미등록| H[로그 경고 후 무시]

  E --> E1[JWT 검증]
  E1 -->|실패| E2["SLogin(LoginInvalidToken)\n→ Disconnect"]
  E1 -->|성공| E3[Redis: Active 입장권 확인]
  E3 -->|없음| E4["SLogin(LoginNotInQueue)\n→ Disconnect"]
  E3 -->|있음| E5[Redis: SET NX 분산 락]
  E5 -->|락 실패 = 중복 로그인| E6["SLogin(LoginDuplicate)\n→ Disconnect"]
  E5 -->|락 성공| E7[GameRoom.Enter(session)]
  E7 --> E8["SLogin(LoginSuccess) 응답"]

  F --> F1["room.Push(JobQueue)"]
  F1 --> F2["room.Broadcast(SMove)\n→ 방의 모든 세션에 전송"]

  G --> G1[GameRoomManager.FindRoom]
  G1 -->|없음| G2["SEnterRoom(NotFound)"]
  G1 -->|있음| G3["currentRoom.Leave\n→ newRoom.Enter\n→ SEnterRoom(Success)"]

  style E2 fill:#ffcccc
  style E4 fill:#ffcccc
  style E6 fill:#ffcccc
  style G2 fill:#ffcccc
  style E8 fill:#ccffcc
  style F2 fill:#ccffcc
  style G3 fill:#ccffcc
```

---

## 5. 세션 종료 플로우

```mermaid
sequenceDiagram
  participant G as Game Server
  participant R as Redis

  Note over G: TCP 연결 종료 감지 (OnDisconnected)
  G->>G: session.Room?.Push(room.Leave(session))
  G->>+R: Lua: GET player:login_lock:{playerId}<br/>→ guid 일치 확인 → DEL
  Note over G,R: 내 락만 해제 (Fire-and-Forget)
  R-->>-G: 1 (삭제됨)
```
