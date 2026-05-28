# Ticketing (Queue) API

게임 서버 입장 대기열 진입·상태·이탈을 담당하는 서비스입니다.

| 항목 | 값 |
|------|-----|
| 개발 환경 기본 URL | `https://localhost:7003` |
| 런타임 | .NET 8.0 |
| 인증 방식 | Bearer Token (JWT) — Authorization 헤더 필요 |

---

## 공통 오류 응답 형식

```json
{ "message": "설명 메시지" }
```

---

## 엔드포인트

### POST /api/queue/enter

**Rate Limit**: 적용 (`queue` 정책)

**응답 200**

> 응답 JSON 형식은 구현 코드 또는 Swagger(`/swagger`)를 참조하세요.

**오류 코드**

| 코드 | 상황 |
|------|------|
| 400 | 입력 검증 실패 또는 잘못된 요청 |
| 401 | 유효하지 않거나 만료된 토큰 |
| 429 | Rate Limit 초과 |

---

### GET /api/queue/status

**Rate Limit**: 적용 (`queue` 정책)

**응답 200**

> 응답 JSON 형식은 구현 코드 또는 Swagger(`/swagger`)를 참조하세요.

**오류 코드**

| 코드 | 상황 |
|------|------|
| 401 | 유효하지 않거나 만료된 토큰 |
| 404 | 리소스를 찾을 수 없음 |
| 429 | Rate Limit 초과 |

---

### POST /api/queue/leave

**Rate Limit**: 적용 (`queue` 정책)

**응답 200**

> 응답 JSON 형식은 구현 코드 또는 Swagger(`/swagger`)를 참조하세요.

**오류 코드**

| 코드 | 상황 |
|------|------|
| 400 | 입력 검증 실패 또는 잘못된 요청 |
| 401 | 유효하지 않거나 만료된 토큰 |
| 404 | 리소스를 찾을 수 없음 |
| 429 | Rate Limit 초과 |

---


> **이 파일은 `.github/scripts/generate_api_docs.py`로 자동 생성됩니다.** 수동 편집 내용은 다음 실행 시 덮어씌워집니다. 내용 변경이 필요하면 컨트롤러 XML 주석 또는 스크립트를 수정하세요.
