# 요구사항 명세: PrepareDbPrimaryPhaseC

작성일: 2026-06-10
브랜치: 2026-06-10_PrepareDbPrimaryPhaseC
소스: task JSON summary + 사용자 계획 문서 (PLAN_2026-06-10_AI_SDLC_PhaseB_to_PhaseC.md)

## 요구사항 요약

`db_write.py`의 `ai_job_steps` 컬럼명 오류(`name` → `step_name`)로 인해 `check_sdlc_consistency --strict`가 FAIL 상태다. 이를 수정하고 PostgreSQL 백업 정책·model_run LEGACY 처리·DB 기반 cost-log 생성 스크립트를 추가하여 Phase B를 완전히 안정화한 뒤 Phase C 전환 조건을 문서화한다.

## 상세 요구사항

### 1. db_write.py 버그 수정 (최우선)

- `action_insert_step`: INSERT 컬럼 `name` → `step_name`
- `action_get_gates`: WHERE 절 `s.name` → `s.step_name`
- 수정 후 Sprint #50 gate 재동기화:
  - `upsert-job --test-generated --review-completed` 실행

### 2. check_sdlc_consistency.py LEGACY 처리

- `model_run_missing` 16건은 consume_tokens=None (cost 추적 이전 레거시)
- `--strict` 모드에서 consume_tokens=None인 job의 model_run_missing은 WARN 아닌 LEGACY로 처리
- LEGACY는 critical count에 포함하지 않음
- 출력 레이블: `LEGACY (no cost tracking)` 명시

### 3. PostgreSQL 백업 정책 수립

- `backup_sdlc_db.sh` 신규 작성:
  - `pg_dump platforma_sdlc` → `AI/backups/YYYY-MM-DD_sdlc.sql.gz`
  - 7일 이상 된 백업 자동 삭제
  - 실행 실패 시 stderr에 경고, exit 0 (흐름 차단 금지)
- `Docs/operations/ai-sdlc-db-migration-roadmap.md` 조건 4번 ✅ 처리
- `.gitignore`에 `AI/backups/*.sql.gz` 추가 (바이너리 커밋 금지)

### 4. generate_cost_log_from_db.py 신규 작성

- PostgreSQL `sdlc.ai_model_runs` + `sdlc.ai_jobs` JOIN
- markdown 형식 cost-log 생성 (Summary / By Sprint / Details 섹션)
- `--dry-run` 모드: stdout만 출력
- `--output` 옵션: 파일 저장
- DB 연결 실패 시 graceful skip (exit 0)
- 첫 출력 대상: `AI/reports/generated-cost-log-from-db.md`

### 5. Phase C 전환 조건 문서화

- `Docs/operations/ai-sdlc-db-migration-roadmap.md` Phase C 섹션 갱신:
  - 30일 대신 기술 조건 기반 전환 기준으로 대체
  - 체크리스트 형태로 구체화
- `Docs/operations/ai-sdlc-phase-c-db-only-plan.md` 신규 작성:
  - Phase C.1 (DB Report 생성) / C.2 (파일 append 제거) / C.3 (Gate DB only) / C.4 (Job Lock 설계) 단계 기술

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|------|---------|
| `.github/scripts/db_write.py` | 버그 수정 (2줄) |
| `.github/scripts/check_sdlc_consistency.py` | LEGACY 처리 로직 추가 |
| `.github/scripts/generate_cost_log_from_db.py` | 신규 작성 |
| `.github/scripts/backup_sdlc_db.sh` | 신규 작성 |
| `Docs/operations/ai-sdlc-db-migration-roadmap.md` | Phase C 조건 갱신 + 조건 4번 완료 |
| `Docs/operations/ai-sdlc-phase-c-db-only-plan.md` | 신규 작성 |
| `.gitignore` | AI/backups/*.sql.gz 추가 |
| `AI/reports/generated-cost-log-from-db.md` | 신규 (생성 결과물) |

**C# 코드 변경 없음** — Python 스크립트·문서만 변경

## 제약 및 주의사항

- ADR-009(PostgreSQL SDLC DB)와 일치: 기존 인프라 범위 내 작업
- `backup_sdlc_db.sh`는 로컬 개발 환경 기준 — 운영 배포 백업 정책과 별개
- `generate_cost_log_from_db.py`는 현재 `AI/cost-log.md` 직접 append를 **대체하지 않음** (Phase C.2에서 처리)
- `AI/backups/` 디렉토리는 `.gitignore` 처리 필수

## 구현 접근 방향

1. `db_write.py` 2줄 수정 → Sprint #50 upsert-job 재실행으로 gate 재동기화
2. `check_sdlc_consistency.py`에 DB join으로 consume_tokens 확인 후 LEGACY 분류
3. `backup_sdlc_db.sh` 작성 + `.gitignore` 추가 → 로드맵 조건 4번 ✅
4. `generate_cost_log_from_db.py` 작성 + dry-run 실행으로 출력 검증
5. 로드맵/Phase C 문서 업데이트

## 검증 기준

- `python .github/scripts/db_write.py --action get-gates --branch 2026-06-08_MigrateToDbPrimary` → 정상 출력
- `python .github/scripts/check_sdlc_consistency.py --check --strict` → exit 0 (FAIL 없음)
- model_run_missing 16건이 LEGACY로 표시되어 critical count에 미포함
- `python .github/scripts/generate_cost_log_from_db.py --dry-run` → markdown 출력
- `bash .github/scripts/backup_sdlc_db.sh` → AI/backups/ 에 .sql.gz 생성
- `dotnet build PlatformA.sln` 오류 0개
- `dotnet test PlatformA.sln` 실패 0개
