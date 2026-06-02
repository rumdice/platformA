# Auth API

인증 및 JWT 토큰 생명주기를 담당하는 서비스입니다.

| 항목 | 값 |
|------|-----|
| 개발 환경 기본 URL | `https://localhost:7001` |
| 런타임 | .NET 10.0 |
| 인증 방식 | Bearer Token (JWT) — /api/Auth/login·logout·refresh 는 토큰 불필요 |

---

## 공통 오류 응답 형식

```json
{ "message": "설명 메시지" }
```

---

## 엔드포인트

### POST /api/auth/login

신규 유저는 자동 등록, 기존 유저는 비밀번호 검증 후 Access Token(15분) + Refresh Token(7일)을 함께 발급합니다.

**Rate Limit**: 적용 (`login` 정책)

**요청 Body**

| 필드 | 타입 | 필수 | 제약 조건 |
|------|------|------|-----------|
| username | string | ✓ | 3~20자, 정규식: `^[a-zA-Z0-9_]+$` |
| password | string | ✓ | 6~100자 |

```json
{
  "username": "<username>",
  "password": "<password>"
}
```

**응답 200**

> 응답 JSON 형식은 구현 코드 또는 Swagger(`/swagger`)를 참조하세요.

**오류 코드**

| 코드 | 상황 |
|------|------|
| 401 | 유효하지 않거나 만료된 토큰 |
| 429 | Rate Limit 초과 |

---

### POST /api/auth/refresh

Refresh Token으로 새 Access Token을 발급합니다. Token Rotation: 기존 Refresh Token을 폐기하고 새 Refresh Token도 함께 발급합니다.

**요청 Body**

| 필드 | 타입 | 필수 | 제약 조건 |
|------|------|------|-----------|
| refreshToken | string | ✓ | — |

```json
{
  "refreshToken": "<refreshToken>"
}
```

**응답 200**

> 응답 JSON 형식은 구현 코드 또는 Swagger(`/swagger`)를 참조하세요.

**오류 코드**

| 코드 | 상황 |
|------|------|
| 401 | 유효하지 않거나 만료된 토큰 |

---

### POST /api/auth/logout

Refresh Token을 Redis에서 즉시 삭제하여 강제 무효화합니다. Access Token은 자연 만료(15분)를 기다립니다.

**요청 Body**

| 필드 | 타입 | 필수 | 제약 조건 |
|------|------|------|-----------|
| refreshToken | string | ✓ | — |

```json
{
  "refreshToken": "<refreshToken>"
}
```

**응답 200**

> 응답 JSON 형식은 구현 코드 또는 Swagger(`/swagger`)를 참조하세요.

**오류 코드**

| 코드 | 상황 |
|------|------|
| 401 | 유효하지 않거나 만료된 토큰 |

---


## 인증 흐름 요약

```
클라이언트                         Auth API                        Redis
    │                                 │                              │
    │── POST /login ──────────────────►│                              │
    │                                 │── SAVE refresh_token ───────►│
    │◄─ { token, refreshToken } ──────│                              │
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


> **이 파일은 `.github/scripts/generate_api_docs.py`로 자동 생성됩니다.** 수동 편집 내용은 다음 실행 시 덮어씌워집니다. 내용 변경이 필요하면 컨트롤러 XML 주석 또는 스크립트를 수정하세요.
