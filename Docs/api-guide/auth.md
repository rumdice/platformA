# Auth API

인증 및 JWT 토큰 생명주기를 담당하는 서비스입니다.

| 항목 | 값 |
|------|-----|
| 개발 환경 기본 URL | `https://localhost:7001` |
| Docker Compose URL | `https://localhost:7001` |
| 런타임 | .NET 8.0 |
| 인증 방식 | JWT (Access Token 15분, Refresh Token 7일) |

---

## 공통 오류 응답 형식

```json
{ "message": "설명 메시지" }
```

---

## 엔드포인트

### POST /api/Auth/login

신규 유저는 자동으로 등록되며, 기존 유저는 비밀번호 검증 후 Access Token과 Refresh Token을 함께 발급합니다.

**Rate Limit**: 10회/분 (IP 단위)

**요청 Body**

| 필드 | 타입 | 필수 | 제약 조건 |
|------|------|------|-----------|
| username | string | ✓ | 3~20자, 영문/숫자/밑줄(_)만 허용 |
| password | string | ✓ | 6~100자 |

```json
{
  "username": "player01",
  "password": "mypassword"
}
```

**응답 200**

```json
{
  "success": true,
  "token": "<access_token>",
  "refreshToken": "<refresh_token>",
  "playerId": 12345,
  "message": "로그인 성공"
}
```

**오류 코드**

| 코드 | 상황 |
|------|------|
| 400 | 입력 검증 실패 (username/password 형식 오류) |
| 401 | 비밀번호 불일치 |
| 429 | Rate Limit 초과 (10회/분) |

---

### POST /api/Auth/refresh

Refresh Token으로 새 Access Token과 새 Refresh Token을 발급합니다.
Token Rotation 방식을 적용하여 기존 Refresh Token은 즉시 폐기됩니다.
동시 요청에 의한 경쟁 조건은 Redis 원자적 GET+DEL 연산으로 방지합니다.

**요청 Body**

| 필드 | 타입 | 필수 | 설명 |
|------|------|------|------|
| refreshToken | string | ✓ | 로그인 시 발급받은 Refresh Token |

```json
{
  "refreshToken": "<refresh_token>"
}
```

**응답 200**

```json
{
  "token": "<new_access_token>",
  "refreshToken": "<new_refresh_token>"
}
```

**오류 코드**

| 코드 | 상황 |
|------|------|
| 400 | refreshToken 필드 누락 |
| 401 | 유효하지 않거나 이미 사용된 Refresh Token, 또는 만료된 토큰 |

---

### POST /api/Auth/logout

Refresh Token을 Redis에서 즉시 삭제하여 강제 무효화합니다.
Access Token은 별도 블랙리스트 없이 자연 만료(15분)를 기다립니다.

**요청 Body**

| 필드 | 타입 | 필수 | 설명 |
|------|------|------|------|
| refreshToken | string | ✓ | 폐기할 Refresh Token |

```json
{
  "refreshToken": "<refresh_token>"
}
```

**응답 200**

```json
{ "message": "로그아웃 완료." }
```

**오류 코드**

| 코드 | 상황 |
|------|------|
| 400 | refreshToken 필드 누락 |
| 401 | 유효하지 않거나 이미 만료된 Refresh Token |

---

## 인증 흐름 요약

```
클라이언트                         Auth API                        Redis
    │                                 │                              │
    │── POST /login ──────────────────►│                              │
    │                                 │── SAVE refresh_token ───────►│
    │◄─ { token, refreshToken } ──────│                              │
    │                                 │                              │
    │   (15분 후 Access Token 만료)    │                              │
    │                                 │                              │
    │── POST /refresh ────────────────►│                              │
    │                                 │── GET+DEL refresh_token ────►│
    │◄─ { token, refreshToken } ──────│── SAVE new_refresh_token ───►│
    │                                 │                              │
    │── POST /logout ─────────────────►│                              │
    │                                 │── GET+DEL refresh_token ────►│
    │◄─ { message: "로그아웃 완료." } ─│                              │
```

> Access Token 검증은 각 API 서비스가 JWT 서명을 직접 확인합니다. Auth API에 재요청하지 않습니다.
