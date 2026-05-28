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

_엔드포인트가 없거나 파싱에 실패했습니다._

> **이 파일은 `.github/scripts/generate_api_docs.py`로 자동 생성됩니다.** 수동 편집 내용은 다음 실행 시 덮어씌워집니다. 내용 변경이 필요하면 컨트롤러 XML 주석 또는 스크립트를 수정하세요.
