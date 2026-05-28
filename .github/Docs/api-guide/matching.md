# Matching API

1v1 게임 매칭 대기열 및 주식 주문 처리를 담당하는 서비스입니다.
SignalR Hub(`/hubs/matching`)를 통해 매칭 결과(MatchFound / MatchTimeout)를 Push합니다.

| 항목 | 값 |
|------|-----|
| 개발 환경 기본 URL | `https://localhost:7002` |
| 런타임 | .NET 9.0 |
| 인증 방식 | Bearer Token (JWT) — Authorization 헤더 필요 |

---

## 공통 오류 응답 형식

```json
{ "message": "설명 메시지" }
```

---

## 엔드포인트

_엔드포인트가 없거나 파싱에 실패했습니다._

## SignalR 이벤트

| 이벤트 | 발생 조건 | 페이로드 |
|--------|----------|----------|
| `MatchFound` | 매칭 성사 | `{ gameServerIp, gameServerPort, roomId }` |
| `MatchTimeout` | 대기 120초 초과 | _(없음)_ |


> **이 파일은 `.github/scripts/generate_api_docs.py`로 자동 생성됩니다.** 수동 편집 내용은 다음 실행 시 덮어씌워집니다. 내용 변경이 필요하면 컨트롤러 XML 주석 또는 스크립트를 수정하세요.
