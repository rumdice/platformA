# Ticketing API

게임 서버 입장을 위한 대기열(Queue) 관리 서비스입니다.
Redis Sorted Set 기반으로 대기열 순서를 관리하며, 입장 가능 상태가 되면 SignalR로 클라이언트에 실시간 알림을 보냅니다.

| 항목 | 값 |
|------|-----|
| 개발 환경 기본 URL | `https://localhost:7003` |
| Docker Compose URL | `https://localhost:7003` |
| 런타임 | .NET 8.0 |
| SignalR Hub | `/hubs/queue` |

---

## 공통 규칙

- 모든 엔드포인트에 `Authorization: Bearer <access_token>` 헤더가 필요합니다.
- Rate Limit: 5회/초 (IP 단위, 전 엔드포인트 공통)
- JWT에서 userId를 추출하여 처리합니다. 요청 Body가 없습니다.

**오류 응답 형식**

```json
{ "message": "설명 메시지" }
```

---

## 엔드포인트

### POST /api/queue/enter

대기열에 진입합니다. 번호표를 발급받습니다.
이미 진입한 유저가 재요청해도 멱등성이 보장됩니다.

**요청**: Body 없음 (JWT에서 userId 추출)

**응답 200**

```
"대기열 등록 완료. UserId: 12345"
```

**오류 코드**

| 코드 | 상황 |
|------|------|
| 400 | 대기열 초과 (최대 10,000명) |
| 401 | 유효하지 않은 토큰 |
| 429 | Rate Limit 초과 |

---

### GET /api/queue/status

현재 대기열 순위와 상태를 조회합니다.
클라이언트는 이 엔드포인트를 주기적으로 폴링하며, `nextPollDelay` 값을 사용하여 다음 폴링 간격을 동적으로 조정해야 합니다.

**요청**: Body 없음 (JWT에서 userId 추출)

**응답 200 — Active (입장 가능 상태)**

```json
{
  "userId": 12345,
  "rank": 0,
  "status": "Active",
  "nextPollDelay": 0
}
```

**응답 200 — Waiting (대기 중)**

```json
{
  "userId": 12345,
  "rank": 42,
  "status": "Waiting",
  "nextPollDelay": 3000
}
```

> `nextPollDelay`: 클라이언트가 다음 폴링 요청을 보내기 전 대기해야 할 시간(ms). 순위에 따라 서버가 동적으로 계산합니다.

**폴링 딜레이 계산 기준**

| 대기 순위 | nextPollDelay |
|-----------|--------------|
| > 100위 | 10,000ms (10초) |
| > 50위 | 5,000ms (5초) |
| > 10위 | 3,000ms (3초) |
| 1~10위 | max(10ms, 1000 / sqrt(rank+1)) |

**오류 코드**

| 코드 | 상황 |
|------|------|
| 401 | 유효하지 않은 토큰 |
| 404 | 대기열 정보 없음 (재진입 필요) |
| 429 | Rate Limit 초과 |

---

### POST /api/queue/leave

대기열에서 명시적으로 이탈합니다.

**요청**: Body 없음 (JWT에서 userId 추출)

**응답 200 — 정상 이탈**

```json
{ "message": "대기열 이탈 완료. UserId: 12345" }
```

**오류 코드**

| 코드 | 상황 |
|------|------|
| 400 | 이미 입장권이 발급된 상태. 게임 서버에 접속하거나 입장권 TTL 만료를 기다려야 함 |
| 401 | 유효하지 않은 토큰 |
| 404 | 대기열에 없는 유저 |
| 429 | Rate Limit 초과 |

---

## SignalR Hub: /hubs/queue

대기열 입장 가능 여부를 실시간으로 수신하기 위한 WebSocket 허브입니다.
클라이언트는 연결 후 `QueueActivated` 이벤트를 대기합니다.

**연결 URL**
```
wss://localhost:7003/hubs/queue
```

**서버 → 클라이언트 이벤트**

| 이벤트 이름 | 데이터 | 의미 |
|-------------|--------|------|
| `QueueActivated` | `{ userId, message }` | 입장권 발급 완료. 게임 서버 접속 가능 상태 |

---

## 대기열 처리 흐름

```
클라이언트                    Ticketing API                      Redis
    │                              │                               │
    │── POST /enter ───────────────►│                               │
    │                              │── ZADD queue ────────────────►│
    │◄─ "대기열 등록 완료" ──────────│                               │
    │                              │                               │
    │   (폴링 루프 시작)            │                               │
    │── GET /status (반복) ─────────►│                               │
    │                              │── ZSCORE / ZRANK ────────────►│
    │◄─ { rank, status, delay } ───│                               │
    │                              │                               │
    │   (백그라운드 워커가 활성화)   │                               │
    │◄── SignalR: QueueActivated ──│                               │
    │                              │                               │
    │   (게임 서버 TCP 접속)        │                               │
```

> Ghost 유저(하트비트 응답 없는 유저)는 백그라운드 워커가 주기적으로 정리합니다.
