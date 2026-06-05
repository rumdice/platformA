# 요구사항 명세: PostgresPrimaryMigration

작성일: 2026-06-05
브랜치: 2026-06-05_PostgresPrimaryMigration
소스: task JSON summary + SPRINT.md

## 요구사항 요약

task JSON 파일을 secondary로 강등하고 PostgreSQL `sdlc.ai_jobs` / `sdlc.ai_job_steps` 테이블을
primary 상태 저장소로 격상한다. 7개 SDLC 스킬에 dual-write를 추가하고, /pr 게이트 검사를
DB SELECT 우선 + 파일 fallback 방식으로 전환한다.

## 상세 요구사항

1. **`db_write.py` 신규 작성** (`.github/scripts/`)
   - 액션 종류:
     - `upsert-job`: ai_jobs INSERT ON CONFLICT(branch) DO UPDATE
     - `insert-step`: ai_job_steps INSERT (job_id는 branch로 조회)
     - `get-gates`: 게이트 검사용 필드 조회 (stdout 출력)
   - 인수:
     - `--action` (필수)
     - `--branch` (필수)
     - `--sprint`, `--task`, `--status`, `--created-at` (upsert-job 용)
     - `--step-name`, `--step-status`, `--step-summary`, `--started-at`, `--completed-at` (insert-step 용)
   - `get-gates` 출력 형식 (stdout, 각 줄):
     ```
     test_generated=true
     review_completed=false
     impact_done=true
     requirement_done=true
     adr_required=false
     ```
   - DB 연결 실패 시 stderr 경고 후 exit 0 (파일 흐름 차단 금지)
   - Python 3.9 호환, `SDLC_DB_CONNECTION` 환경변수 사용

2. **`/plan` SKILL.md 수정**
   - task JSON 커밋 완료 후 `upsert-job` 호출 추가
   - status="analyzing", sprint, task, branch, created_at 전달

3. **`/start` SKILL.md 수정**
   - task JSON status→"coding" 갱신 후 `upsert-job` 호출 추가 (status="coding")

4. **`/done` SKILL.md 수정**
   - steps[] 배열 기록 후 `insert-step` 호출 추가 (name="done")

5. **`/pr` SKILL.md 수정 (2곳)**
   - 3단계(task JSON status=done) 갱신 후 `upsert-job` 호출 추가 (status="done")
   - 게이트 검사: DB `get-gates` 우선, DB 실패 시 기존 grep fallback 유지

6. **`/test-gen` SKILL.md 수정**
   - steps[] 기록 후 `insert-step` 호출 추가 (name="test_gen")

7. **`/review` SKILL.md 수정**
   - steps[] 기록 후 `insert-step` 호출 추가 (name="review")

8. **`/impact` SKILL.md 수정**
   - steps[] 기록 후 `insert-step` 호출 추가 (name="impact")

## 영향 범위 (예상)

- `.github/scripts/db_write.py` — 신규 (Python, ~150줄)
- `.claude/skills/plan/SKILL.md` — upsert-job 호출 추가
- `.claude/skills/start/SKILL.md` — upsert-job 호출 추가
- `.claude/skills/done/SKILL.md` — insert-step 호출 추가
- `.claude/skills/pr/SKILL.md` — upsert-job 호출 + get-gates 게이트 검사 추가
- `.claude/skills/test-gen/SKILL.md` — insert-step 호출 추가
- `.claude/skills/review/SKILL.md` — insert-step 호출 추가
- `.claude/skills/impact/SKILL.md` — insert-step 호출 추가
- C# 코드 변경 없음

## 제약 및 주의사항

- ADR-009: sdlc 스키마, `SDLC_DB_CONNECTION` 환경변수 규칙 준수
- 모든 DB 호출은 `|| true` — 연결 실패 시 파일 기반 흐름 차단 금지
- ai_jobs: UNIQUE(branch) 인덱스 — ON CONFLICT DO UPDATE 사용
- ai_job_steps: job_id는 branch로 ai_jobs 조회
- 게이트 검사: DB 응답이 빈 문자열이면 grep fallback 실행
- `migrate_tasks_to_postgres.py --apply` — 기존 JSON 파일들을 DB에 이전하는 별도 작업 (이 PR에서는 실행 안 함)

## 구현 접근 방향

1. `db_write.py`: parse_conn/get_conn 패턴은 migrate_tasks_to_postgres.py와 동일하게 재사용
2. upsert-job: `INSERT INTO sdlc.ai_jobs (branch, sprint_number, task_name, status, created_at) ON CONFLICT (branch) DO UPDATE SET status=EXCLUDED.status, updated_at=NOW()`
3. insert-step: ai_jobs.id 조회 → ai_job_steps INSERT (ON CONFLICT IGNORE)
4. get-gates: SELECT 한 번으로 필요 필드 모두 반환, stdout으로 key=value 형식 출력
5. 각 SKILL.md: 기존 파일 기반 코드 아래에 `db_write.py ... || true` 호출 1줄 추가

## 검증 기준

- `/plan` 실행 후 `SELECT * FROM sdlc.ai_jobs ORDER BY id DESC LIMIT 1;` 으로 행 확인
- `/pr` 게이트 검사: DB 사용 가능 시 SQL 결과 사용, `psycopg2` 미설치 시 grep fallback 동작
- 빌드 오류 없음 (C# 변경 없으므로 기존 133개 테스트 통과 유지)
