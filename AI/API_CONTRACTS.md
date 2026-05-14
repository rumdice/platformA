# API_CONTRACTS — API 명세

> API 구현 전 반드시 이 파일을 먼저 업데이트하십시오.
> 모든 엔드포인트는 실제 컨트롤러 코드와 동기화되어야 합니다.

---

## 공통 규칙

### 인증
모든 보호된 엔드포인트는 HTTP 헤더 필요:
```
Authorization: Bearer <access_token>
```

### 표준 에러 응답 포맷
```json
{ "message": "설명 메시지" }
```

### HTTP 상태 코드
| 코드 | 의미 |
|------|------|
| 200 | 성공 |
| 400 | 잘못된 요청 (입력 오류, 비즈니스 규칙 위반) |
| 401 | 인증 실패 (토큰 없음/만료/무효) |
| 404 | 리소스 없음 |
| 429 | Rate Limit 초과 |
| 500 | 서버 내부 오류 |

---

## Auth API (포트: 7001)

Base URL: `https://localhost:7001/api/Auth`

### POST /login
신규 유저 자동 등록 + JWT 발급. 기존 유저는 비밀번호 검증 후 발급.

**Rate Limit**: 10회/분 (IP 단위)

**Request:**
```json
{
  "username": "string (3~20자, 영문/숫자/밑줄)",
  "password": "string (6~100자)"
}
```

**Response 200:**
```json
{
  "success": true,
  "token": "<access_token>",
  "refreshToken": "<refresh_token>",
  "playerId": 12345,
  "message": "로그인 성공"
}
```

**Response 401:** 비밀번호 불일치
**Response 429:** Rate Limit 초과

---

### POST /refresh
Refresh Token → 새 Access Token + 새 Refresh Token 발급 (Token Rotation).
기존 Refresh Token은 즉시 폐기.

**Request:**
```json
{
  "refreshToken": "<refresh_token>"
}
```

**Response 200:**
```json
{
  "token": "<new_access_token>",
  "refreshToken": "<new_refresh_token>"
}
```

**Response 401:** 유효하지 않거나 이미 사용된 Refresh Token

---

### POST /logout
Refresh Token을 Redis에서 즉시 삭제. Access Token은 자연 만료(15분) 대기.

**Request:**
```json
{
  "refreshToken": "<refresh_token>"
}
```

**Response 200:**
```json
{ "message": "로그아웃 완료." }
```

**Response 401:** 유효하지 않은 Refresh Token

---

## Ticketing API (포트: 7003)

Base URL: `https://localhost:7003/api/queue`

모든 엔드포인트 **Rate Limit**: 5회/초 (IP 단위)
모든 엔드포인트 **인증 필요**: `Authorization: Bearer <token>`

### POST /enter
대기열 진입 (번호표 발급). 이미 진입한 경우 멱등성 보장.

**Request:** Body 없음 (JWT에서 userId 추출)

**Response 200:**
```
"대기열 등록 완료. UserId: 12345"
```

**Response 400:** 대기열 초과 (최대 10,000명)
**Response 401:** 유효하지 않은 토큰

---

### GET /status
현재 대기열 순위 및 상태 조회. 폴링 간격 동적 조정.

**Request:** Body 없음

**Response 200 (Active — 입장 가능):**
```json
{
  "userId": 12345,
  "rank": 0,
  "status": "Active",
  "nextPollDelay": 0
}
```

**Response 200 (Waiting — 대기 중):**
```json
{
  "userId": 12345,
  "rank": 42,
  "status": "Waiting",
  "nextPollDelay": 3000
}
```
> `nextPollDelay`: 클라이언트가 다음 폴링까지 대기할 ms. 순위에 따라 동적 조정.

**Response 404:** 대기열 정보 없음 (재진입 필요)
**Response 401:** 유효하지 않은 토큰

### 폴링 딜레이 계산 기준
| 순위 | 딜레이 |
|------|--------|
| > 100 | 10,000ms |
| > 50 | 5,000ms |
| > 10 | 3,000ms |
| ≤ 10 | max(10ms, 1000/√(rank+1)) |

---

### POST /leave
대기열 명시적 이탈.

**Request:** Body 없음

**Response 200:** 이탈 완료
**Response 400:** 이미 입장권 발급된 상태 (게임 서버 접속 또는 TTL 만료 대기)
**Response 404:** 대기열에 없는 유저

---

### SignalR Hub: /hubs/queue
실시간 대기열 알림용 WebSocket 허브.

| 이벤트 | 방향 | 데이터 |
|--------|------|--------|
| `QueueActivated` | 서버→클라이언트 | `{ userId, message }` |

---

## Matching API (포트: 7002)

Base URL: `https://localhost:7002/api/GameMatch`

### POST /RequestMatch
매칭 대기열 진입 요청. 즉시 응답 (매칭 결과는 SignalR로 비동기 수신).

**인증 필요**: `Authorization: Bearer <token>`

**Request:** Body 없음

**Response 200:**
```json
{ "message": "매칭 대기열에 성공적으로 진입했습니다." }
```

**Response 401:** 유효하지 않은 토큰

---

### DELETE /CancelMatch
매칭 취소. 대기열에서 본인을 제거합니다.

**인증 필요**: `Authorization: Bearer <token>`

**Request:** Body 없음

**Response 200:**
```json
{ "message": "매칭이 취소되었습니다." }
```

**Response 404:** 대기열에서 찾을 수 없는 경우

---

### GET /Status
대기열 순위 조회. 본인의 순위와 전체 대기 인원을 반환합니다.

**인증 필요**: `Authorization: Bearer <token>`

**Response 200:**
```json
{ "rank": 3, "total": 10 }
```

**Response 404:** 대기열에 없는 경우

---

### SignalR Hub: /hubs/matching
매칭 결과 실시간 알림.

| 이벤트 | 방향 | 데이터 |
|--------|------|--------|
| `MatchFound` | 서버→클라이언트 | `{ roomId, matchedUserIds }` |
| `MatchTimeout` | 서버→클라이언트 | `{ message }` (120초 초과 시) |

**CORS 허용 오리진:**
- `http://127.0.0.1:5500`
- `http://localhost:5500`
- `http://localhost:8080`

---

## Utils API (포트: 7004)

Base URL: `http://localhost:7004`

### GET /util/myip
클라이언트 공인 IP 조회.

**Response 200:**
```json
{
  "ip": "1.2.3.4",
  "city": "Seoul",
  "region": "KR",
  "country_name": "South Korea",
  "org": "My Level2 Server",
  "latitude": 37.5665,
  "longitude": 126.9780
}
```

---

### POST /util/shorten
URL 단축. Snowflake ID → Base62 변환으로 고유 코드 생성.

**Request:**
```json
{ "url": "https://example.com/very/long/path" }
```

**Response 200:**
```json
{
  "shortUrl": "http://localhost/go/Abc123",
  "code": "Abc123"
}
```

**Response 400:** 유효하지 않은 URL

---

### GET /go/{code}
단축 URL 리다이렉트. Write-Back 패턴으로 클릭 수 Redis에 비동기 기록.

**Response 302:** 원본 URL로 리다이렉트
**Response 404:** 존재하지 않는 코드

---

### GET /util/stats/{code}
단축 URL 클릭 통계.

**Response 200:**
```json
{
  "code": "Abc123",
  "originalUrl": "https://example.com/...",
  "clickCount": 42,
  "createdAt": "2026-04-21T00:00:00Z"
}
```

**Response 404:** 존재하지 않는 코드

---

## Game Server TCP 프로토콜 (포트: 7777)

Binary 패킷. 자세한 내용은 `AI/PATTERNS.md` 참조.

### 패킷 ID 목록

| ID | 방향 | 이름 | 페이로드 크기 |
|----|------|------|-------------|
| 1 | C→S | C_Move | 12 bytes (float X, Y, Z) |
| 2 | S→C | S_Move | 16 bytes (int PlayerId, float X, Y, Z) |
| 3 | C→S | C_Login | 가변 |
| 4 | S→C | S_Login | 가변 |
| 5 | C→S | C_EnterRoom | 가변 |
| 6 | S→C | S_EnterRoom | 가변 |

### 헤더 구조
```
[0..1] Length (ushort, LE) — 헤더 포함 전체 크기
[2..3] PacketID (ushort, LE)
[4..N] Payload
```
