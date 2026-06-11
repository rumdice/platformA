# AI_SDLC DB Schema

PostgreSQL 데이터베이스: `platforma_sdlc`, 스키마: `sdlc`

EF Core 마이그레이션: `PlatformA.SdlcDB.Lib/Migrations/`

---

## sdlc.ai_jobs

SDLC 작업(스프린트 단위 task)의 주 테이블.
Phase C 이후 task JSON 파일 대신 이 테이블이 단일 진실 공급원이다.

| 컬럼 | 타입 | 설명 |
|------|------|------|
| `id` | bigint (PK) | 자동 증가 |
| `sprint` | int? | 스프린트 번호 |
| `task_name` | varchar NOT NULL | PlanName (PascalCase) |
| `branch` | varchar NOT NULL UNIQUE | git 브랜치명 |
| `status` | varchar NOT NULL | `analyzing` / `coding` / `testing` / `done` / `failed` |
| `pr_url` | varchar? | GitHub PR URL |
| `test_generated` | bool | `/test-gen` 완료 여부 |
| `review_completed` | bool | `/review` 완료 여부 |
| `impact_done` | bool | `/impact` 완료 여부 |
| `requirement_done` | bool | `/requirement` 완료 여부 |
| `adr_required` | bool | DESIGN_REVIEW에서 ADR 필요 판정 여부 |
| `duration_sec` | int? | 총 작업 시간(초) |
| `consume_tokens` | bigint? | 소비 토큰 수 |
| `cache_tokens` | bigint? | 캐시 토큰 수 |
| `retry_count` | int | 자동 수정 재시도 횟수 |
| `last_error` | text? | 마지막 오류 메시지 |
| `source_json` | text? | task JSON 원본 (Phase A/B 마이그레이션용) |
| `impact` | text? | /impact 분석 결과 JSON |
| `locked_by` | varchar? | Job Lock 소유자. NULL=미잠금 |
| `locked_at` | timestamptz? | Lock 획득 시각 |
| `lock_expires_at` | timestamptz? | Lock 만료 시각 |
| `lock_token` | varchar? | Release/heartbeat 검증용 UUID |
| `agent_id` | varchar? | 실행 주체 (claude-code, n8n 등) |
| `last_heartbeat_at` | timestamptz? | 마지막 heartbeat 시각 |
| `created_at` | timestamptz | 작업 시작 시각 |
| `completed_at` | timestamptz? | 작업 완료 시각 |
| `inserted_at` | timestamptz | DB 최초 INSERT 시각 |
| `updated_at` | timestamptz | 마지막 UPDATE 시각 |

### 주요 제약

```sql
UNIQUE (branch)
INDEX (sprint)
INDEX (status)
```

### gate 조회 쿼리

```sql
SELECT test_generated, review_completed, impact_done, requirement_done, adr_required
FROM sdlc.ai_jobs
WHERE branch = 'my-branch';
```

---

## sdlc.ai_job_steps

각 스킬 실행의 단계별 기록.

| 컬럼 | 타입 | 설명 |
|------|------|------|
| `id` | bigint (PK) | 자동 증가 |
| `job_id` | bigint FK | `ai_jobs.id` 참조 |
| `step_name` | varchar NOT NULL | 단계명 (requirement, impact, start, test_gen, done, review, pr 등) |
| `status` | varchar NOT NULL | `done` / `failed` / `skipped` |
| `summary` | text? | 단계 요약 1문장 |
| `started_at` | timestamptz? | 단계 시작 시각 |
| `completed_at` | timestamptz? | 단계 완료 시각 |
| `duration_sec` | int? | 단계 소요 시간(초) |
| `result_json` | text? | 단계 결과 JSON (선택) |
| `created_at` | timestamptz | 레코드 생성 시각 |

---

## sdlc.ai_model_runs

LLM 토큰 사용량 상세 기록.

| 컬럼 | 타입 | 설명 |
|------|------|------|
| `id` | bigint (PK) | 자동 증가 |
| `job_id` | bigint? FK | `ai_jobs.id` 참조 (nullable) |
| `step_id` | bigint? FK | `ai_job_steps.id` 참조 (nullable) |
| `model_name` | varchar? | 사용한 모델명 (예: claude-sonnet-4-6) |
| `provider` | varchar? | 공급자 (anthropic 등) |
| `input_tokens` | bigint? | 입력 토큰 수 |
| `output_tokens` | bigint? | 출력 토큰 수 |
| `cache_read_tokens` | bigint? | 캐시 읽기 토큰 수 |
| `cache_creation_tokens` | bigint? | 캐시 생성 토큰 수 |
| `total_tokens` | bigint? | 합계 토큰 수 |
| `estimated_cost` | decimal? | 추정 비용 (USD) |
| `started_at` | timestamptz? | 모델 호출 시작 시각 |
| `completed_at` | timestamptz? | 모델 호출 완료 시각 |
| `raw_usage` | text? | API 원본 usage 응답 JSON |
| `created_at` | timestamptz | 레코드 생성 시각 |

---

## sdlc.ai_failures

CI 실패 및 auto-fix 실패 기록.

| 컬럼 | 타입 | 설명 |
|------|------|------|
| `id` | bigint (PK) | 자동 증가 |
| `job_id` | bigint? FK | `ai_jobs.id` 참조 (nullable) |
| `failure_type` | varchar NOT NULL | `format_failed` / `build_failed` / `test_failed` / `sdlc_gate_failed` 등 |
| `source` | varchar NOT NULL | 실패 발생 위치 (github_actions, local 등) |
| `message` | text? | 오류 메시지 요약 |
| `log_excerpt` | text? | 로그 발췌 (최대 2000자) |
| `fixable_by_ai` | bool? | AI 자동 수정 가능 여부 |
| `retry_count` | int | 자동 수정 재시도 횟수 |
| `resolved` | bool | 해결 여부 |
| `resolved_at` | timestamptz? | 해결 시각 |
| `created_at` | timestamptz | 기록 시각 |
| `metadata` | text? | 추가 메타데이터 JSON |
| `git_hub_run_id` | bigint? | GitHub Actions run ID |
| `git_hub_job_id` | bigint? | GitHub Actions job ID |
| `workflow_name` | varchar? | 워크플로 이름 |
| `commit_sha` | varchar? | 커밋 SHA |
| `branch` | varchar? | 브랜치명 |

### 중복 방지 인덱스

```sql
CREATE UNIQUE INDEX ux_ai_failures_github_job_failure
ON sdlc.ai_failures (git_hub_run_id, git_hub_job_id, failure_type)
WHERE git_hub_run_id IS NOT NULL AND git_hub_job_id IS NOT NULL;
```

---

## Sprint Sequence

```sql
-- 스프린트 번호 원자적 발급 (/plan에서 사용)
SELECT nextval('sdlc.sprint_seq');
```

---

## EF Core 마이그레이션 이력

| Migration | 날짜 | 내용 |
|-----------|------|------|
| `InitialSdlcDb` | 2026-06-03 | 4개 테이블 + sprint_seq 초기 생성 |
| `AddGitHubFailureIdentity` | 2026-06-05 | ai_failures에 git_hub_run_id, git_hub_job_id, workflow_name, commit_sha, branch 추가 + partial unique index |

### Migration 적용

```bash
cd PlatformA/PlatformA.SdlcDB.Lib
dotnet ef database update --context SdlcDbContext
```

---

## 관련 문서

- [Phase C DB 단독 운영](phase-c-db-only.md)
- [Job Lock 정책](job-lock.md)
- [n8n 실패 모니터](n8n.md)
- [Backup / Restore](backup-restore.md)
