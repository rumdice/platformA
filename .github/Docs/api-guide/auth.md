# Auth API

인증 및 JWT 토큰 생명주기를 담당하는 서비스입니다.

| 항목 | 값 |
|------|-----|
| 개발 환경 기본 URL | `https://localhost:7001` |
| 런타임 | .NET 8.0 |
| 인증 방식 | Bearer Token (JWT) — /api/Auth/login·logout·refresh 는 토큰 불필요 |

---

## 공통 오류 응답 형식

```json
{ "message": "설명 메시지" }
```

---

## 엔드포인트

_엔드포인트가 없거나 파싱에 실패했습니다._

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
