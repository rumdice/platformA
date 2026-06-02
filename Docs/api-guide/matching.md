# Matching API

1v1 게임 매칭 대기열 및 주식 주문 처리를 담당하는 서비스입니다.
SignalR Hub(`/hubs/matching`)를 통해 매칭 결과(MatchFound / MatchTimeout)를 Push합니다.

| 항목 | 값 |
|------|-----|
| 개발 환경 기본 URL | `https://localhost:7002` |
| 런타임 | .NET 10.0 |
| 인증 방식 | Bearer Token (JWT) — Authorization 헤더 필요 |

---

## 공통 오류 응답 형식

```json
{ "message": "설명 메시지" }
```

---

## 엔드포인트

### POST /api/gamematch/RequestMatch

매칭 요청: Bearer JWT로 인증 후 매칭 대기열에 진입합니다.

**응답 200**

> 응답 JSON 형식은 구현 코드 또는 Swagger(`/swagger`)를 참조하세요.

**오류 코드**

| 코드 | 상황 |
|------|------|
| 401 | 유효하지 않거나 만료된 토큰 |

---

### DELETE /api/gamematch/CancelMatch

매칭 취소: 대기열에서 본인을 제거합니다.

**응답 200**

> 응답 JSON 형식은 구현 코드 또는 Swagger(`/swagger`)를 참조하세요.

**오류 코드**

| 코드 | 상황 |
|------|------|
| 401 | 유효하지 않거나 만료된 토큰 |

---

### GET /api/gamematch/Status

대기열 상태 조회: 본인의 순위와 전체 대기 인원을 반환합니다.

**응답 200**

> 응답 JSON 형식은 구현 코드 또는 Swagger(`/swagger`)를 참조하세요.

**오류 코드**

| 코드 | 상황 |
|------|------|
| 401 | 유효하지 않거나 만료된 토큰 |
| 404 | 리소스를 찾을 수 없음 |

---

### POST /api/orders

**요청 Body**

| 필드 | 타입 | 필수 | 제약 조건 |
|------|------|------|-----------|
| type | int (0=Buy, 1=Sell) |  | — |
| price | decimal |  | — |
| quantity | long |  | — |

```json
{
  "type": 0,
  "price": 0.0,
  "quantity": 0
}
```

**응답 202**

> 응답 JSON 형식은 구현 코드 또는 Swagger(`/swagger`)를 참조하세요.

---


## SignalR 이벤트

| 이벤트 | 발생 조건 | 페이로드 |
|--------|----------|----------|
| `MatchFound` | 매칭 성사 | `{ gameServerIp, gameServerPort, roomId }` |
| `MatchTimeout` | 대기 120초 초과 | _(없음)_ |


> **이 파일은 `.github/scripts/generate_api_docs.py`로 자동 생성됩니다.** 수동 편집 내용은 다음 실행 시 덮어씌워집니다. 내용 변경이 필요하면 컨트롤러 XML 주석 또는 스크립트를 수정하세요.
