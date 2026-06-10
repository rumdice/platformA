# 요구사항 명세: AddSdlcWorkflowTests

작성일: 2026-06-11
브랜치: 2026-06-11_AddBranchConflictGuard
소스: 직접 입력 (workflow 인수)

## 요구사항 요약

AI_SDLC 파이프라인의 정책-구현 불일치(auto-fix job lock 미연동)를 해소하고,
job_lock/DB 파이프라인/Migration에 대한 자동화 pytest를 도입하여
파이프라인 회귀를 자동으로 감지할 수 있게 한다.

## 상세 요구사항

### 1. auto-fix.yml + /qa-failure 스킬 job_lock 연동 (P0 — 정책 완성)

**배경**: `ai-sdlc-auto-fix-policy.md` Section 8에 "lock claim 필수"라고 명시되어 있으나
`auto-fix.yml`과 `/qa-failure` 스킬 모두 `job_lock.py` 호출이 없는 불일치 상태.

**요구사항**:
- `auto-fix.yml`에 스텝 추가:
  - `/qa-failure` 실행 전: `job_lock.py claim --branch BRANCH --agent-id n8n --ttl 30`
  - lock 획득 실패(exit 1) 시: PR comment 남기고 워크플로우 실패 처리
  - `always()` 조건으로 release 스텝 추가 (성공/실패 무관하게 실행)
  - `SDLC_DB_CONNECTION` secret 사용
- `LOCK_TOKEN`을 `GITHUB_ENV`로 전파하여 release 스텝에서 사용
- `/qa-failure` 스킬에는 GitHub Actions 환경 전용이므로 SKILL.md 변경 없음
  (n8n → auto-fix.yml 경로에서만 lock이 필요, 로컬 /qa-failure 실행은 lock 불필요)

### 2. job_lock.py pytest 자동화 (P1)

**파일**: `.github/scripts/tests/test_job_lock.py`

**8개 테스트 시나리오**:
1. `test_claim_unlocked_job` — 미잠금 job에 claim 성공 → exit 0
2. `test_claim_nonexistent_branch` — 존재하지 않는 branch → exit 2
3. `test_claim_already_locked_by_other` — 다른 owner 점유 중 → exit 1
4. `test_claim_same_owner_reentry` — 동일 owner 재진입 → exit 0 (갱신)
5. `test_claim_stale_lock` — TTL 만료된 lock → exit 0 (덮어쓰기)
6. `test_release_wrong_token` — 잘못된 token으로 release → exit 1
7. `test_heartbeat_extends_ttl` — heartbeat 후 lock_expires_at 연장 확인
8. `test_expire_clears_stale_locks` — expire 명령으로 만료 lock 일괄 해제

**설계 원칙**:
- 테스트용 더미 branch명: `test-jl-{uuid4 앞 8자리}` — 실제 sprint와 충돌 없음
- 각 테스트는 `setup`에서 테스트 branch를 ai_jobs에 INSERT, `teardown`에서 DELETE
- psycopg2 미설치 또는 DB 미연결 시 `pytest.skip` (로컬 환경 선택적 실행)
- `@pytest.mark.integration` 마커로 분류

### 3. AI_SDLC 파이프라인 E2E smoke test (P2)

**파일**: `.github/scripts/tests/test_sdlc_pipeline.py`

**3개 시나리오**:
1. `test_job_lifecycle` — upsert-job(analyzing) → insert-step × 3 → get-gates → assert all false 후 true 순서 검증
2. `test_lock_and_pipeline_integration` — claim → step 기록 → release → list-active(0) 확인
3. `test_gate_enforcement` — test_generated=false 상태에서 get-gates 결과가 false 반환 확인

**정리**: 테스트 완료 후 `DELETE FROM sdlc.ai_jobs WHERE branch LIKE 'test-smoke-%'`

### 4. Migration 멱등성 검증 + Down SQL (P3)

**파일**: `.github/scripts/tests/test_migration_idempotency.py`

**요구사항**:
- `sdlc_db_migrations.sql` 전체를 2회 실행해도 오류 없음 (`IF NOT EXISTS` 검증)
- Migration 004 컬럼 존재 여부를 `information_schema.columns`로 확인

**sdlc_db_migrations.sql**:
- 각 Migration 블록 뒤에 Down(rollback) SQL을 주석으로 추가

### 5. sdlc-python-test.yml GitHub Actions 워크플로우 (P1)

**파일**: `.github/workflows/sdlc-python-test.yml`

**트리거**: `push` 또는 `pull_request` — `.github/scripts/tests/**` 변경 시
**환경**: `ubuntu-latest`, psycopg2-binary 설치, PostgreSQL service container
**실행**: `pytest .github/scripts/tests/ -m integration -v`

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|------|----------|
| `.github/workflows/auto-fix.yml` | lock claim/release 스텝 추가 |
| `.github/workflows/sdlc-python-test.yml` | 신규 |
| `.github/scripts/tests/test_job_lock.py` | 신규 |
| `.github/scripts/tests/test_sdlc_pipeline.py` | 신규 |
| `.github/scripts/tests/test_migration_idempotency.py` | 신규 |
| `.github/scripts/tests/__init__.py` | 신규 (빈 파일) |
| `.github/scripts/sdlc_db_migrations.sql` | Down SQL 주석 추가 |

## 제약 및 주의사항

- `SDLC_DB_CONNECTION` secret이 GitHub Actions에 등록되어 있어야 pytest 워크플로우 동작
- PostgreSQL service container: `postgres:15`, `platforma_sdlc` DB 생성 + Migration 적용 필요
- 테스트 브랜치명은 반드시 `test-` 접두사 사용 — 실제 sprint DB와 충돌 방지
- auto-fix.yml lock claim 실패 시 PR comment 남기는 로직은 이미 정책 문서에 명시됨 (`gh pr comment`)
- C# 코드 변경 없음 → dotnet build/test 영향 없음

## 구현 접근 방향

1. **auto-fix.yml**: `pip install psycopg2-binary` 스텝 추가 후 claim → /qa-failure → release(always) 순서
2. **pytest 파일**: `conftest.py`에 DB 연결 fixture 중앙화, 각 테스트는 fixture 주입 방식
3. **sdlc-python-test.yml**: PostgreSQL service container로 격리된 테스트 DB 사용
4. **Migration idempotency**: `sdlc_db_migrations.sql`을 그대로 실행하여 검증

## 검증 기준

- `auto-fix.yml` 수동 트리거(repository_dispatch) 후 `job_lock.py list-active`에 n8n lock 표시됨
- `pytest .github/scripts/tests/ -m integration` 전체 통과
- `sdlc_db_migrations.sql` 2회 연속 실행 오류 없음
- PR #87(현재 브랜치)에 두 기능(branch conflict guard + workflow tests) 합산 반영

## DESIGN_REVIEW 결과

| ADR | 관련 여부 | 충돌/참고 사항 |
|-----|---------|--------------|
| ADR-008: n8n 이벤트 오케스트레이터 | 관련 있음 | n8n → auto-fix 경로에 lock 추가, 기존 설계와 일치 |
| ADR-009: PostgreSQL SDLC DB | 관련 있음 | pytest가 sdlc DB에 직접 접근, 기존 설계 범위 내 |
| 기타 ADR | 없음 | Python/YAML 변경, 인프라 변경 없음 |

판정: ✅ 기존 ADR 준수 — 신규 ADR 불필요
- pytest는 새 외부 서비스가 아닌 테스트 도구
- PostgreSQL service container는 CI 전용 임시 인스턴스
- 기존 아키텍처에 없던 설계 결정 없음
