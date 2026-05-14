# DOMAIN — 게임/비즈니스 도메인 규칙

> 이 문서는 "왜 이렇게 동작하는가"를 설명합니다.
> 코드 변경 전 관련 도메인 규칙을 반드시 확인하십시오.

---

## 1. 사용자 인증 흐름

### 로그인 (신규/기존 유저 통합)
```
클라이언트 → POST /api/Auth/login
        ↓
   Username 존재 여부 확인 (MySQL)
        ├── 없음 → 자동 등록 (BCrypt 해싱 후 INSERT)
        └── 있음 → BCrypt 비밀번호 검증
              ├── 실패 → 401 Unauthorized
              └── 성공 ↓
                   Access Token (JWT, 15분) 발급
                   Refresh Token (랜덤, 7일) 발급
                   Refresh Token → Redis 저장
```

**규칙:**
- 신규 유저는 별도 회원가입 없이 최초 로그인 시 자동 생성
- 비밀번호 변경 미지원 (현재)
- 동일 Username 중복 불가 (DB 유니크 인덱스)

### Token Rotation
```
클라이언트 → POST /api/Auth/refresh (Refresh Token 전송)
        ↓
   Redis에서 Refresh Token 원자적 GET+DEL
        ├── 없음 (만료/사용됨) → 401
        └── 있음 → 새 Access Token + 새 Refresh Token 발급
                    새 Refresh Token → Redis 저장
```

**규칙:**
- Refresh Token은 1회용 (사용 즉시 폐기)
- 동시 Refresh 요청이 와도 한 번만 성공 (GET+DEL 원자성)
- Access Token 블랙리스트 없음 → 만료(15분) 대기

---

## 2. 대기열 시스템

### 진입 흐름
```
클라이언트 → POST /api/queue/enter
        ↓
   Rate Limit 확인 (5req/s)
        ↓
   JWT 검증 → userId 추출
        ↓
   Heartbeat 갱신 (현재 시각 ZSet에 기록)
        ↓
   이미 대기열에 있는지 확인 (멱등성)
        ├── 있음 → 기존 순위 반환 (중복 등록 없음)
        └── 없음 → ZSet ZADD (score = 진입 시각 Unix timestamp)
              ├── 대기열 초과 (10,000명) → 400
              └── 성공 → 200
```

### Ghost 유저 감지
```
QueueWorkerService (백그라운드)
        ↓
   주기적으로 Heartbeat ZSet 스캔
        ↓
   마지막 Heartbeat > TTL(300초) 경과한 유저 탐지
        ↓
   Ghost 유저 → 대기열에서 제거
```

**규칙:**
- Heartbeat는 `/api/queue/status` 호출마다 갱신
- 5분간 폴링하지 않으면 자동 이탈 (Ghost 처리)
- 대기열 최대 크기: 10,000명 (`Consts.WAIT_QUEUE_MAX_SIZE`)

### Active 상태 (입장권)
```
QueueWorkerService
        ↓
   대기열 앞 N명 → Active 상태로 전환
        ├── Redis Hash에 Active 정보 저장 (TTL: 300초)
        ├── 대기열 ZSet에서 제거
        └── SignalR로 해당 유저에게 "QueueActivated" 알림
```

**규칙:**
- Active 상태 유저는 Game Server에 직접 TCP 접속 가능
- Active TTL(300초) 내 접속하지 않으면 입장권 자동 소멸
- Game Server 로그인 시 Active 키를 즉시 삭제 (소비)

---

## 3. 매칭 시스템

### 매칭 흐름
```
클라이언트 → POST /api/GameMatch/RequestMatch
        ↓
   JWT 검증 → playerId 추출
        ↓
   Redis 매칭 큐에 추가
        ↓
   200 OK 즉시 반환 (비동기)
        ↓
[백그라운드] EngineService
        ↓
   매칭 큐에서 2명 추출
        ↓
   매칭 성공 → Redis Pub/Sub 발행 (match_success_channel)
        ↓
   SignalR로 양 유저에게 "MatchFound" 알림
        ↓
[Game Server] match_success_channel 구독
        ↓
   GameRoomManager.CreateRoom(roomId, [userId1, userId2])
```

**규칙:**
- 현재 1:1 (2인) 매칭만 지원
- 매칭 큐는 FIFO (선착순)
- Rating 기반 매칭 미구현
- 매칭 성공 후 Game Server 접속은 클라이언트 책임

---

## 4. Game Server 세션 흐름

### 연결 ~ 게임 시작
```
클라이언트 → TCP Connect (포트 7777)
        ↓
   GameSession 생성
        ↓
   C_Login 패킷 수신 (playerId, active_token)
        ↓
   Redis에서 Active 키 확인 (ticket:active:user:{playerId})
        ├── 없음 → S_Login(ResultNotInQueue) + Disconnect
        └── 있음 → Active 키 삭제 (소비)
              ↓
         분산 락 획득 (player:login_lock:{playerId})
              ├── 실패 (이미 접속) → S_Login(ResultDuplicate) + Disconnect
              └── 성공 → S_Login(ResultSuccess) + GameRoom 배정
```

**규칙:**
- Active 키 없이는 Game Server 입장 불가 (티켓팅 시스템 우회 방지)
- 동일 playerId 중복 접속 불가 (분산 락)
- 락은 연결 유지 중 계속 갱신 (TTL: 1일)
- 연결 종료 시 락 해제 + GameRoom에서 제거

### 패킷 처리 스레드 안전성
```
GameSession.OnReceive()  ← 네트워크 I/O 스레드
        ↓
   PacketManager.HandlePacket()
        ↓
   GameRoom.Push(action)  ← 액션을 JobQueue에 enqueue
        ↓
[GameRoom Worker Thread] JobQueue.Dequeue() + action()
```

**규칙:**
- 모든 게임 상태 변경은 반드시 `room.Push()` 통해서만 수행
- 네트워크 스레드에서 직접 게임 상태 수정 금지 (레이스 컨디션)

---

## 5. 데이터 정합성 규칙

### 플레이어 통계
- `player_stats` 테이블은 `players`와 1:1 관계
- 플레이어 생성 시 `player_stats` 동시 생성 (트랜잭션)
- 통계 업데이트는 매치 종료 시점에만 수행

### 매치 기록
- `match_records.status` 변경 순서: Pending → InProgress → Completed/Cancelled
- Player1 또는 Player2 삭제 시 매치 기록 보존 (`DeleteBehavior.Restrict`)
- Winner 삭제 시 winner_id = NULL으로 설정 (`DeleteBehavior.SetNull`)

### Redis 키 네이밍 규칙
```
{ticket:queue}:global          — 대기열 ZSet (해시 태그로 슬롯 고정)
{ticket:queue}:heartbeats      — 하트비트 ZSet
ticket:active:user:{userId}    — 입장권 Hash
refresh_token:{token}          — Refresh Token 문자열
player:login_lock:{playerId}   — 분산 락 문자열
url:{code}                     — URL 캐시 문자열 (Utils)
stats:{code}                   — 클릭 통계 정수 (Utils)
dirty_codes                    — Write-Back 대상 Set (Utils)
```

**규칙:**
- `{ticket:queue}` 해시 태그 필수 (같은 Redis 슬롯 보장)
- 새 Redis 키 추가 시 반드시 `Consts.cs`에 상수로 등록
- TTL 없는 키는 원칙적으로 사용 금지 (예외: 게임 서버 분산 락)
