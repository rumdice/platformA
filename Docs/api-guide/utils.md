# Utils API

독립적인 유틸리티 기능을 제공하는 서비스입니다.
URL 단축, IP 조회, 클릭 통계를 처리하며, 다른 게임/인증 서비스와 독립적으로 운영됩니다.

| 항목 | 값 |
|------|-----|
| 개발 환경 기본 URL | `http://localhost:<launchSettings 포트>` |
| Docker Compose URL | `https://localhost:7004` |
| 런타임 | .NET 8.0 |
| 데이터 저장소 | SQLite (`app.db`), Redis (캐시) |

---

## 공통 오류 응답 형식

```json
{ "message": "설명 메시지" }
```

---

## 엔드포인트

### GET /util/myip

클라이언트의 공인 IP 주소를 조회합니다.
리버스 프록시 환경에서는 `X-Forwarded-For` 헤더를 우선 확인하며, 로컬호스트 접속 시 외부 API(`api.ipify.org`)를 통해 서버의 공인 IP를 반환합니다.

**인증**: 불필요

**응답 200**

```json
{
  "ip": "1.2.3.4",
  "city": "Seoul (Controller Ver.)",
  "region": "KR",
  "country_name": "South Korea",
  "org": "My Level2 Server",
  "latitude": 37.5665,
  "longitude": 126.9780
}
```

> `city`, `org` 필드는 현재 더미 값입니다. 실제 GeoIP API 연동이 예정되어 있습니다.

---

### POST /util/shorten

긴 URL을 단축합니다.
Snowflake ID를 Base62로 변환하여 고유한 단축 코드를 생성합니다. 중복 없는 분산 ID 생성으로 B-tree 인덱스 성능이 우수합니다.

**인증**: 불필요

**요청 Body**

| 필드 | 타입 | 필수 | 설명 |
|------|------|------|------|
| url | string | ✓ | 단축할 원본 URL (절대 경로 형식) |

```json
{
  "url": "https://example.com/very/long/path?param=value"
}
```

**응답 200**

```json
{
  "shortUrl": "https://localhost:7004/go/Abc123",
  "code": "Abc123"
}
```

**오류 코드**

| 코드 | 상황 |
|------|------|
| 400 | 유효하지 않은 URL (빈 값 또는 절대 경로가 아닌 형식) |

---

### GET /go/{code}

단축 코드로 원본 URL로 리다이렉트합니다.
Write-Back 패턴으로 클릭 수를 Redis에 비동기 기록합니다. 백그라운드 서비스(`StatSyncService`)가 주기적으로 Redis의 클릭 수를 SQLite DB에 반영합니다.

**인증**: 불필요

**경로 파라미터**

| 파라미터 | 타입 | 설명 |
|----------|------|------|
| code | string | 단축 코드 (예: `Abc123`) |

**응답 302** — 원본 URL로 리다이렉트

**오류 코드**

| 코드 | 상황 |
|------|------|
| 404 | 존재하지 않는 단축 코드 |

---

### GET /util/stats/{code}

단축 URL의 클릭 통계를 조회합니다.
Redis에 최신 클릭 수가 있으면 Redis 값을 반환하고, 없으면 SQLite DB 값을 반환합니다.

**인증**: 불필요

**경로 파라미터**

| 파라미터 | 타입 | 설명 |
|----------|------|------|
| code | string | 단축 코드 (예: `Abc123`) |

**응답 200**

```json
{
  "code": "Abc123",
  "originalUrl": "https://example.com/very/long/path",
  "clickCount": 42,
  "createdAt": "2026-04-21T00:00:00Z"
}
```

**오류 코드**

| 코드 | 상황 |
|------|------|
| 404 | 존재하지 않는 단축 코드 |

---

## 클릭 수 Write-Back 흐름

```
클라이언트            Utils API            Redis               SQLite
    │                    │                   │                    │
    │── GET /go/Abc123 ──►│                   │                    │
    │                    │── GET url:Abc123 ──►│                   │
    │                    │◄─ (캐시 히트) ──────│                   │
    │                    │── INCR stats:Abc123 ►│                  │
    │                    │── SADD dirty_codes ──►│                 │
    │◄─ 302 리다이렉트 ───│                   │                    │
    │                    │                   │                    │
    │   (백그라운드 StatSyncService)          │                    │
    │                    │── SMEMBERS dirty_codes ►│               │
    │                    │── GET stats:Abc123 ──►│                 │
    │                    │── UPDATE click_count ─────────────────►│
    │                    │── SREM dirty_codes ──►│                │
```

> 캐시 미스 시: SQLite에서 조회 후 Redis에 URL(TTL 10분)과 클릭 수를 함께 캐싱합니다.
