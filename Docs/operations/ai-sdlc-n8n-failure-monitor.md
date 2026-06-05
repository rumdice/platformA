# AI SDLC — n8n GitHub 실패 모니터 운영 가이드

## 1. 목적

GitHub Actions CI 실패를 n8n이 자동으로 감지하여 PostgreSQL `sdlc.ai_failures` 테이블에 기록한다.
중복 기록은 partial unique index로 방지한다.

## 2. 구성요소

```
GitHub Actions CI (ci.yml)
    ↓ 실패 발생
n8n: GitHub CI Failure Monitor (10분 폴링)
    └─ GitHub API: /actions/runs?status=failure
    └─ GitHub API: /actions/runs/{id}/jobs
    └─ 실패 유형 분류 (JS Code 노드)
    └─ PostgreSQL: sdlc.ai_failures INSERT
         (ON CONFLICT DO NOTHING — 중복 방지)
```

## 3. 워크플로 노드 설명

| 순서 | 노드명 | 역할 |
|-----|--------|------|
| 1 | Schedule (10분) | 10분마다 자동 실행 트리거 |
| 2 | GitHub: 실패한 CI 조회 | `status=failure` runs 최대 5개 조회 |
| 3 | 각 실패 run 분리 | workflow_runs 배열을 개별 항목으로 분리 |
| 4 | 최근 15분 이내만 | 15분 이내 실패만 통과 (오래된 실패 중복 방지) |
| 5 | GitHub: Job 상세 조회 | 각 run의 jobs/steps 상세 조회 |
| 6 | 실패 분류 | format/style/test/gate/build로 분류, git_hub_run_id 추출 |
| 7 | PostgreSQL: ai_failures INSERT | ON CONFLICT 중복 방지 INSERT |

## 4. 중복 방지 메커니즘

### Partial Unique Index

```sql
CREATE UNIQUE INDEX ux_ai_failures_github_job_failure
ON sdlc.ai_failures (git_hub_run_id, git_hub_job_id, failure_type)
WHERE git_hub_run_id IS NOT NULL AND git_hub_job_id IS NOT NULL;
```

- `git_hub_run_id + git_hub_job_id + failure_type` 조합이 동일하면 중복으로 간주
- WHERE 조건: `git_hub_run_id IS NOT NULL` → GitHub Actions 실패에만 적용
- 로컬 수동 기록(`github_run_id=NULL`)은 이 constraint 미적용 → 항상 INSERT

### ON CONFLICT 구문

```sql
ON CONFLICT (git_hub_run_id, git_hub_job_id, failure_type)
WHERE git_hub_run_id IS NOT NULL AND git_hub_job_id IS NOT NULL
DO NOTHING
```

같은 run/job의 같은 실패 유형이 10분마다 재감지되어도 DB row가 1개만 유지된다.

## 5. Credential 설정

### GitHub PAT (n8n UI)

1. n8n UI → Settings → Credentials → New
2. Type: **HTTP Header Auth**
3. Name: `GitHub PAT`
4. Header Name: `Authorization`
5. Header Value: `Bearer ghp_...` (GitHub Personal Access Token)
6. 필요 권한: `repo`, `actions:read`

### SDLC PostgreSQL (n8n UI)

1. n8n UI → Settings → Credentials → New
2. Type: **Postgres**
3. Name: `SDLC PostgreSQL`
4. Host: `postgres` (Docker 네트워크) 또는 `localhost` (로컬 직접)
5. Port: `5432`
6. Database: `platforma_sdlc`
7. User: `platforma`
8. Password: `platforma_dev_password`
9. Schema: `sdlc`

## 6. 워크플로 Import 방법

```
n8n UI → Workflows → + New → ⋮ (더보기) → Import from File
파일: .n8n/workflows/github-failure-monitor.json
```

Import 후:
1. PostgreSQL 노드 클릭 → Credential 재선택 (`SDLC PostgreSQL`)
2. GitHub 노드 두 곳 클릭 → Credential 재선택 (`GitHub PAT`)
3. 우측 상단 Toggle → **Active** 로 전환

## 7. 수동 실행 절차

1. n8n UI → Workflows → GitHub CI Failure Monitor
2. 우측 상단 **Test Workflow** 버튼 클릭
3. Executions 탭에서 각 노드 결과 확인
4. PostgreSQL INSERT 노드: `rows affected` 확인

## 8. 데이터 조회

```sql
-- 최근 ai_failures 확인
SELECT id, failure_type, git_hub_run_id, git_hub_job_id, branch,
       workflow_name, resolved, created_at
FROM sdlc.ai_failures
ORDER BY created_at DESC
LIMIT 10;

-- 미해결 실패만
SELECT failure_type, branch, message, created_at
FROM sdlc.ai_failures
WHERE resolved = false
ORDER BY created_at DESC;

-- 중복 방지 인덱스 확인
SELECT indexname, indexdef
FROM pg_indexes
WHERE schemaname = 'sdlc' AND tablename = 'ai_failures';

-- ai_jobs 현황
SELECT sprint, task_name, branch, status
FROM sdlc.ai_jobs
ORDER BY sprint DESC
LIMIT 10;
```

## 9. 로컬 수동 기록 (record_failure.py)

GitHub Actions 없이 로컬에서 실패를 기록할 때:

```bash
# 기본 (github_run_id 없음 → ON CONFLICT 미적용)
python .github/scripts/record_failure.py \
    --type build_failed \
    --branch my-branch \
    --message "로컬 빌드 실패"

# GitHub identity 포함 (ON CONFLICT 중복 방지 적용)
python .github/scripts/record_failure.py \
    --type format_failed \
    --branch my-branch \
    --message "format 실패" \
    --run-id 12345678 \
    --job-id 87654321 \
    --commit-sha abc123 \
    --workflow "CI — Build & Test"

# 미해결 실패 조회
python .github/scripts/record_failure.py \
    --list-unresolved \
    --branch my-branch

# 해결 처리
python .github/scripts/record_failure.py \
    --resolve \
    --branch my-branch \
    --type format_failed
```

## 10. 현재 한계

| 한계 | 내용 |
|------|------|
| n8n 가동 전제 | n8n이 꺼져 있으면 실패 기록 누락 가능 |
| 자동 수정 범위 | format/style 오류만 auto-format.yml이 자동 수정 |
| build/test 자동 수정 | 미구현 (수동 수정 필요) |
| GitHub Actions → DB | GitHub Actions에서 직접 PostgreSQL에 접근하지 않음 (보안) |
| PR merge 자동 재시도 | 미구현 |

## 11. 트러블슈팅

### PostgreSQL 연결 오류

```
n8n 노드: Connection refused
```
→ Docker Compose 실행 확인: `docker ps | grep sdlc-postgres`
→ `docker/postgresql/docker-compose.yml` 또는 `docker/docker-compose.full.yml`로 재기동

### Credential 재연결 필요

```
n8n 노드: Credential not found
```
→ Import 후 노드를 클릭하여 Credential을 수동으로 재선택

### ON CONFLICT 미동작

```
ai_failures에 같은 run/job 데이터가 반복 INSERT됨
```
→ partial unique index 확인:
```sql
SELECT indexname FROM pg_indexes
WHERE schemaname='sdlc' AND tablename='ai_failures'
AND indexname='ux_ai_failures_github_job_failure';
```
→ 없으면 `dotnet ef database update` 재실행

### text→jsonb 타입 오류

```
column "metadata" cannot be cast automatically to type jsonb
```
→ Migration `AddGitHubFailureIdentity`에 `USING metadata::jsonb` 포함 여부 확인
→ `dotnet ef database update --context SdlcDbContext` 재실행
