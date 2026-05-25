# AI/tasks/ — 작업 상태 JSON 스키마

이 디렉토리는 각 스프린트 작업의 상태를 JSON 파일로 저장한다.
SPRINT.md가 사람이 읽는 형식이라면, tasks/*.json은 스킬이 프로그래밍 방식으로 읽고 쓰는 형식이다.

## 파일 명명 규칙

```
sprint{N}_{PlanName}.json
```

예: `sprint24_AddTestGenSkill.json`, `sprint25_ImproveSdlcGates.json`

## JSON 스키마

```json
{
  "sprint": 25,
  "task": "ImproveSdlcGates",
  "branch": "2026-05-22_ImproveSdlcGates",
  "status": "analyzing",
  "created_at": "2026-05-22T00:00:00Z",
  "completed_at": null,
  "pr_url": null,
  "retry_count": 0,
  "last_error": null,
  "artifacts": [],
  "test_generated": false,
  "review_completed": false,
  "impact": null,
  "steps": []
}
```

## 필드 정의

| 필드 | 타입 | 설명 |
|------|------|------|
| `sprint` | int | 스프린트 번호 |
| `task` | string | 작업 PascalCase 이름 (브랜치명에서 날짜 제거) |
| `branch` | string | 작업 브랜치명 |
| `status` | string | 아래 상태 머신 참조 |
| `created_at` | ISO8601 | /plan 실행 시각 |
| `completed_at` | ISO8601 \| null | /pr 완료 시각 |
| `pr_url` | string \| null | 생성된 PR URL |
| `retry_count` | int | 재시도 횟수 |
| `last_error` | string \| null | 마지막 실패 원인 |
| `artifacts` | string[] | 생성된 주요 파일 목록 |
| `test_generated` | boolean \| false | /test-gen 실행 완료 여부 |
| `review_completed` | boolean \| false | /review 실행 완료 여부 |
| `impact` | object \| null | /impact 실행 결과 (아래 구조 참조) |
| `steps` | object[] | 단계별 실행 이력 (아래 구조 참조) |

### `impact` 필드 구조

```json
"impact": {
  "risk": "LOW | MEDIUM | HIGH",
  "changed_files": 0,
  "high_risk_files": [],
  "medium_risk_files": [],
  "low_risk_files": [],
  "test_coverage": "none | partial | full | not_required",
  "summary": "영향 분석 요약"
}
```

### `steps` 필드 구조

```json
"steps": [
  {
    "name": "impact | test_gen | review | done | pr | gate_check | merge_sync",
    "status": "done | failed | skipped",
    "started_at": "2026-05-22T00:00:00Z",
    "completed_at": "2026-05-22T00:01:00Z",
    "summary": "단계 요약 (예: MEDIUM risk, 5 changed files)"
  }
]
```

향후 `ai_job_steps` 테이블로 마이그레이션 가능한 구조다.

### `steps[]` 권장 step name

| step name | 실행 주체 |
|-----------|----------|
| `requirement` | `/requirement` 스킬 |
| `plan` | `/plan` 스킬 |
| `impact` | `/impact` 스킬 |
| `start` | `/start` 스킬 |
| `test_gen` | `/test-gen` 스킬 |
| `done` | `/done` 스킬 |
| `review` | `/review` 스킬 |
| `pr` | `/pr` 스킬 |
| `gate_check` | GitHub Actions `sdlc-gate-check.yml` |
| `merge_sync` | GitHub Actions `pr-merge-sync.yml` |

## 상태 머신 (6단계)

```
pending → analyzing → coding → testing → done
                                   ↓
                                failed
```

| 상태 | 전환 시점 | 담당 스킬 |
|------|---------|---------|
| `pending` | task JSON 파일 생성 직후 (미시작) | — |
| `analyzing` | /plan 브랜치 생성 완료 | `/plan` |
| `coding` | /start 실행 완료 | `/start` |
| `testing` | 빌드·테스트 통과 (push 후) | `/done` |
| `done` | PR 생성 완료 | `/pr` |
| `failed` | /done 빌드·테스트 실패 | `/done` |

## 스킬 연동

- `/plan` 스킬: 브랜치 생성 후 `status: "analyzing"` 파일 자동 생성
- `/start` 스킬: `status: "coding"` 전환
- `/impact` 스킬: `impact` 필드 갱신 + `steps[]` 기록
- `/test-gen` 스킬: `test_generated: true` 기록 + `steps[]` 기록
- `/review` 스킬: `review_completed: true` 기록 + `steps[]` 기록
- `/done` 스킬: `status: "testing"` 전환 / 실패 시 `status: "failed"`, `last_error` 기록
- `/pr` 스킬: `status: "done"`, `completed_at`, `pr_url` 업데이트 + `steps[]` 기록

## GitHub Actions 연동

다음 워크플로우가 task JSON을 읽거나 갱신한다.

| Workflow | 역할 | 트리거 |
|----------|------|--------|
| `sdlc-gate-check.yml` | PR 생성/수정 시 test/review/impact 게이트 검사 | PR open/sync |
| `pr-merge-sync.yml` | PR merge 후 status=done, completed_at, pr_url, SPRINT, cost-log 동기화 + `steps[]` merge_sync 기록 | PR merged |

### 상태 책임 정리

| 전환 | 담당 |
|------|------|
| `→ analyzing` | `/plan` 스킬 |
| `→ coding` | `/start` 스킬 |
| `→ testing` | `/done` 스킬 |
| `→ done` | `/pr` 스킬 (1차) |
| `→ done` (보정) | `pr-merge-sync.yml` (PR merge 후 `/pr` 미실행 경로) |

## 향후 마이그레이션

Phase 3(PostgreSQL 기반 모니터링) 도입 시:
- `tasks/*.json` → `ai_jobs` 테이블
- `steps[]` 배열 → `ai_job_steps` 테이블 (job_id FK)
