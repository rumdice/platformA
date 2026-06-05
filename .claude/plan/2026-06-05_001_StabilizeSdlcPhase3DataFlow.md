# 요구사항 명세: StabilizeSdlcPhase3DataFlow

작성일: 2026-06-05
브랜치: 2026-06-05_StabilizeSdlcPhase3DataFlow
소스: plan mode (~/.claude/plans/1-tender-cupcake.md)

## 요구사항 요약

2026-06-04에 구축된 Phase 3 인프라(PostgreSQL SdlcDB.Lib + n8n)의 세 가지 핵심 결함을 수정하여
실제 데이터가 중복 없이 흐르는 파이프라인을 완성한다.
기능 확장이 아닌 기존 구현의 안정화가 목표다.

## 상세 요구사항

### R1. ai_failures 중복 방지 (EF Core + PostgreSQL)

**문제**: `ai_failures` 테이블에 unique constraint가 없어 n8n의 `ON CONFLICT DO NOTHING`이 무효.
같은 GitHub Actions run/job 실패가 10분마다 반복 INSERT된다.

**해결**:
1. `AiFailure` Entity에 5개 컬럼 추가:
   - `GitHubRunId long?` → `github_run_id bigint`
   - `GitHubJobId long?` → `github_job_id bigint`
   - `WorkflowName string?` → `workflow_name text`
   - `CommitSha string?` → `commit_sha char(40)`
   - `Branch string?` → `branch varchar(200)`
2. `Metadata` 컬럼 타입 `text` → `jsonb` (n8n이 `$6::jsonb`로 INSERT하므로 타입 일치 필요)
3. Partial unique index 추가:
   ```sql
   CREATE UNIQUE INDEX ux_ai_failures_github_job_failure
   ON sdlc.ai_failures (github_run_id, github_job_id, failure_type)
   WHERE github_run_id IS NOT NULL AND github_job_id IS NOT NULL;
   ```
   이유: 로컬 실패(github_run_id=NULL)와 GitHub Actions 실패를 분리 처리
4. EF Core Migration: `AddGitHubFailureIdentity`

### R2. task JSON → PostgreSQL 실이전 (--apply 구현)

**문제**: `migrate_tasks_to_postgres.py`에 `--apply` 미구현 → `ai_jobs` 테이블에 실데이터 없음.
23개 task JSON 파일이 DB에 반영되지 않아 PostgreSQL이 거울 역할을 못 한다.

**해결**: `--apply` 모드 구현 (psycopg2, record_failure.py와 동일 패턴)
- `ai_jobs`: `ON CONFLICT (branch) DO UPDATE SET ...` upsert (branch unique index 기존 존재)
- `ai_job_steps`: `DELETE WHERE job_id = ? + re-INSERT` (초기 mirror용 단순 전략)
- 반복 실행 안전: 2회 `--apply` 실행해도 row 수 불변
- `RETURNING id`로 job_id 확보 후 steps INSERT에 활용

### R3. record_failure.py GitHub identity 지원

**문제**: 로컬에서 CI 실패를 수동 기록할 때 github_run_id/job_id 미지원 → 중복 방지 불가.

**해결**: optional 인수 4개 추가 (기존 인수 전체 유지, 하위 호환 보장)
- `--run-id int` (github_run_id)
- `--job-id int` (github_job_id)
- `--commit-sha str`
- `--workflow str`
- INSERT에 `ON CONFLICT (github_run_id, github_job_id, failure_type) WHERE NOT NULL DO NOTHING` 추가
- `list_unresolved`/`resolve` 쿼리: `branch` 직접 컬럼 + `metadata->>'branch'` OR 조건으로 하위 호환

### R4. n8n workflow INSERT 컬럼 보완

**문제**: 현재 INSERT에 `github_run_id`/`github_job_id`가 없어 partial unique index와 ON CONFLICT가 매칭되지 않음.

**해결**:
- 실패 분류 노드(JS): `github_run_id`, `github_job_id`, `commit_sha`, `workflow_name`, `branch` 값을 반환 객체에 추가
- PostgreSQL INSERT 노드: 신규 컬럼 포함 + `ON CONFLICT (github_run_id, github_job_id, failure_type) WHERE NOT NULL DO NOTHING`

### R5. 운영 문서 신규 작성

- `Docs/operations/ai-sdlc-n8n-failure-monitor.md`: n8n 워크플로 운영 가이드
- `Docs/operations/toc.yml` 항목 추가

## 영향 범위 (예상)

| 레이어 | 파일 | 변경 성격 |
|-------|------|---------|
| C# Entity | `PlatformA.SdlcDB.Lib/Entities/AiFailure.cs` | 프로퍼티 5개 추가 |
| C# DbContext | `PlatformA.SdlcDB.Lib/SdlcDbContext.cs` | Fluent API 추가 |
| EF Migration | `PlatformA.SdlcDB.Lib/Migrations/` | 신규 migration 파일 |
| Python | `.github/scripts/migrate_tasks_to_postgres.py` | --apply 구현 |
| Python | `.github/scripts/record_failure.py` | 인수·INSERT 확장 |
| JSON | `.n8n/workflows/github-failure-monitor.json` | INSERT 쿼리 수정 |
| 문서 | `Docs/operations/` | md 신규 + toc 수정 |

**기존 게임 서비스 무영향**: Auth/Ticketing/Matching/Game.Server 변경 없음.
**기존 테스트 무영향**: SdlcDB.Lib 전용 테스트 없음 (테스트 불필요 범주).

## 제약 및 주의사항

1. **ADR-009 준수**: PostgreSQL은 `sdlc` 스키마 분리 유지. n8n 메타데이터는 `n8n` 스키마 사용.
2. **ADR-008 준수**: n8n은 감지/기록/오케스트레이션 역할. 자동 코드수정 루프는 이번 범위 외.
3. **소스 of truth 불변**: AI/tasks/*.json이 primary, PostgreSQL은 mirror. task JSON 삭제 금지.
4. **HasFilter() 주의**: EF Core Fluent API의 `HasFilter()` 인수는 raw SQL → snake_case 직접 기재.
5. **text→jsonb 마이그레이션**: 기존 metadata 값이 모두 valid JSON이므로 AlterColumn 안전.
6. **n8n credential**: workflow JSON 업데이트 후 n8n UI에서 PostgreSQL credential 재연결 필요할 수 있음.
7. **psycopg2 전제**: migrate_tasks_to_postgres.py --apply는 `pip install psycopg2-binary` 필요.

## 구현 접근 방향

```
[C# 레이어]
AiFailure.cs 프로퍼티 추가
  → SdlcDbContext.cs Fluent API (HasColumnType jsonb, partial index)
  → dotnet ef migrations add AddGitHubFailureIdentity
  → Migration 파일 검토 (AlterColumn, 5 AddColumn, partial unique index)
  → dotnet ef database update

[Python 레이어]
migrate_tasks_to_postgres.py
  → parse_conn/get_conn 추가 (record_failure.py 패턴 재사용)
  → run_apply() 함수: ai_jobs upsert + ai_job_steps DELETE+INSERT

record_failure.py
  → CLI 인수 4개 추가 (optional, 하위 호환)
  → record() INSERT SQL 확장 + ON CONFLICT 추가
  → list_unresolved/resolve 쿼리 개선

[n8n 레이어]
github-failure-monitor.json
  → 실패 분류 노드 JS: 신규 컬럼 값 반환
  → PostgreSQL INSERT 노드: 컬럼/파라미터/ON CONFLICT 업데이트

[문서 레이어]
Docs/operations/ai-sdlc-n8n-failure-monitor.md 신규
Docs/operations/toc.yml 항목 추가
```

## 검증 기준

| 검증 항목 | 방법 | 기대 결과 |
|---------|------|---------|
| 빌드 | `dotnet build PlatformA.SdlcDB.Lib` | 오류 0 |
| Migration | `dotnet ef migrations list` | AddGitHubFailureIdentity 표시 |
| DB 스키마 | `\d sdlc.ai_failures` | 5개 신규 컬럼 + unique index |
| task 이전 | `--apply` 후 `SELECT COUNT(*) FROM sdlc.ai_jobs` | 23 |
| 중복 방지 | `record_failure.py --run-id 1 --job-id 2` 2회 실행 | DB row 1건 |
| n8n 중복 | workflow 동일 실행 반복 | ai_failures row 증가 없음 |
| 전체 빌드 | `dotnet build PlatformA.sln` | 오류 0 |
| 전체 테스트 | `dotnet test PlatformA.sln` | 전체 통과 |
