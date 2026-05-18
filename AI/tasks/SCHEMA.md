# AI/tasks/ — 작업 상태 JSON 스키마

이 디렉토리는 각 스프린트 작업의 상태를 JSON 파일로 저장한다.
SPRINT.md가 사람이 읽는 형식이라면, tasks/*.json은 스킬이 프로그래밍 방식으로 읽고 쓰는 형식이다.

## 파일 명명 규칙

```
sprint{N}_{PlanName}.json
```

예: `sprint21_AISDLCEnhancements.json`, `sprint22_FixRedisBug.json`

## JSON 스키마

```json
{
  "sprint": 21,
  "task": "AISDLCEnhancements",
  "branch": "2026-05-18_AISDLCEnhancements",
  "status": "in_progress",
  "created_at": "2026-05-18T10:00:00Z",
  "completed_at": null,
  "pr_url": null,
  "retry_count": 0,
  "last_error": null,
  "artifacts": []
}
```

## 필드 정의

| 필드 | 타입 | 설명 |
|------|------|------|
| `sprint` | int | 스프린트 번호 |
| `task` | string | 작업 PascalCase 이름 (브랜치명에서 날짜 제거) |
| `branch` | string | 작업 브랜치명 |
| `status` | string | `pending` \| `in_progress` \| `done` \| `failed` |
| `created_at` | ISO8601 | /plan 실행 시각 |
| `completed_at` | ISO8601 \| null | /done 완료 시각 |
| `pr_url` | string \| null | 생성된 PR URL |
| `retry_count` | int | 재시도 횟수 |
| `last_error` | string \| null | 마지막 실패 원인 |
| `artifacts` | string[] | 생성된 주요 파일 목록 |

## 스킬 연동

- `/plan` 스킬: 브랜치 생성 후 `status: "in_progress"` 파일 자동 생성
- `/done` 스킬: PR 생성 후 `status: "done"`, `completed_at`, `pr_url` 업데이트

## 향후 마이그레이션

Phase 3(PostgreSQL 기반 모니터링) 도입 시 이 JSON 파일들을
`ai_jobs` 테이블로 일괄 마이그레이션할 수 있다.
