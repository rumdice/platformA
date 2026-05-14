# Matching API

2인 매칭을 담당하는 서비스입니다.
클라이언트가 매칭 요청을 보내면 즉시 응답하고, 매칭 성사 결과는 SignalR로 비동기 수신합니다.
매칭이 성사되면 Redis Pub/Sub(`match_success_channel`)을 통해 Game Server에 알림을 발행합니다.

또한 주식 매도/매수 주문 처리(Order Book) 기능도 포함되어 있습니다.

| 항목 | 값 |
|------|-----|
| 개발 환경 기본 URL | `https://localhost:7002` |
| Docker Compose URL | `https://localhost:7002` |
| 런타임 | .NET 9.0 |
| SignalR Hub | `/hubs/matching` |

---

## 공통 오류 응답 형식

```json
{ "message": "설명 메시지" }
```

---

## GameMatch 엔드포인트

### POST /api/GameMatch/RequestMatch

매칭 대기열에 진입합니다.
Bearer JWT로 인증 후 Redis 매칭 큐에 플레이어를 추가합니다.
매칭 결과는 SignalR `/hubs/matching`의 `MatchFound` 이벤트로 수신합니다.

**요청 헤더**: `Authorization: Bearer <access_token>`
**요청 Body**: 없음 (JWT에서 playerId 추출)

**응답 200**

```json
{ "message": "매칭 대기열에 성공적으로 진입했습니다." }
```

**오류 코드**

| 코드 | 상황 |
|------|------|
| 401 | 유효하지 않은 토큰 |

---

### DELETE /api/GameMatch/CancelMatch

매칭 대기열에서 본인을 제거합니다.

**요청 헤더**: `Authorization: Bearer <access_token>`
**요청 Body**: 없음

**응답 200**

```json
{ "message": "매칭이 취소되었습니다." }
```

**오류 코드**

| 코드 | 상황 |
|------|------|
| 401 | 유효하지 않은 토큰 |
| 404 | 대기열에서 찾을 수 없음 |

---

### GET /api/GameMatch/Status

본인의 매칭 대기열 순위와 전체 대기 인원을 반환합니다.

**요청 헤더**: `Authorization: Bearer <access_token>`
**요청 Body**: 없음

**응답 200**

```json
{ "rank": 3, "total": 10 }
```

> `rank`: 현재 내 순위 (1부터 시작). `total`: 전체 대기 인원.

**오류 코드**

| 코드 | 상황 |
|------|------|
| 401 | 유효하지 않은 토큰 |
| 404 | 매칭 대기열에 없음 |

---

## Order 엔드포인트

주식 매도/매수 주문 처리를 위한 Order Book API입니다.

### POST /api/orders

주문을 접수합니다. 서버에서 Snowflake 방식으로 순차 ID를 부여하고 매칭 엔진에 전달합니다.

**요청 Body**

| 필드 | 타입 | 필수 | 설명 |
|------|------|------|------|
| type | int | ✓ | 0: Buy(매수), 1: Sell(매도) |
| price | decimal | ✓ | 주문 단가 |
| quantity | long | ✓ | 주문 수량 |

```json
{
  "type": 0,
  "price": 50000.00,
  "quantity": 10
}
```

**응답 202 Accepted**

```json
{
  "orderId": 1234567890,
  "message": "주문이 접수되었습니다."
}
```

---

### GET /api/orders/book

현재 호가창 상태를 조회합니다 (디버깅용).
실제 데이터는 서버 콘솔(터미널)에 출력됩니다.

**응답 200**

```
"서버 콘솔(터미널)을 확인하세요."
```

---

## SignalR Hub: /hubs/matching

매칭 결과를 실시간으로 수신하기 위한 WebSocket 허브입니다.

**연결 URL**
```
wss://localhost:7002/hubs/matching
```

**CORS 허용 오리진**
- `http://127.0.0.1:5500`
- `http://localhost:5500`
- `http://localhost:8080`

**서버 → 클라이언트 이벤트**

| 이벤트 이름 | 데이터 | 의미 |
|-------------|--------|------|
| `MatchFound` | `{ roomId, matchedUserIds }` | 매칭 성사. 게임 서버의 roomId와 상대방 userId 포함 |
| `MatchTimeout` | `{ message }` | 120초 초과 시 매칭 타임아웃 |

---

## 매칭 처리 흐름

```
클라이언트A                 Matching API                Redis              Game Server
    │                           │                         │                    │
    │── POST /RequestMatch ─────►│                         │                    │
    │                           │── ZADD match_queue ─────►│                    │
    │◄─ 200 OK ─────────────────│                         │                    │
    │                           │                         │                    │
    │   (백그라운드 워커 동작)   │                         │                    │
    │                           │── 2명 감지 → ZPOPMIN ───►│                    │
    │                           │── PUBLISH match_success ─────────────────────►│
    │◄─ SignalR: MatchFound ────│                         │                    │
    │   { roomId, matchedUserIds }                        │                    │
    │                           │                         │                    │
    │   (TCP 접속 시작)          │                         │                    │
    │────────────────────────────────────────────────────────────── TCP ───────►│
```
