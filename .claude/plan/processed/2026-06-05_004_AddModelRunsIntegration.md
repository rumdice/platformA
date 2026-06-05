# 요구사항 명세: AddModelRunsIntegration

작성일: 2026-06-05
브랜치: 2026-06-05_AddModelRunsIntegration
소스: task JSON summary

## 요구사항 요약

`/pr` 스킬 완료 시 `count_tokens.py`가 계산한 토큰 사용량을 `sdlc.ai_model_runs` 테이블에
자동으로 INSERT한다. `cost-log.md` 파일 기록은 병행 유지하며, PostgreSQL 연결 실패 시
경고만 출력하고 cost-log.md 기록은 정상적으로 완료한다.

## 상세 요구사항

1. **insert_model_run.py 신규 작성** (`.github/scripts/`)
   - 인수: `--branch <브랜치명>` (필수), `--created-at <ISO8601>` (필수)
   - count_tokens.py 출력(duration_sec, consume_tokens, cache_tokens)을 읽어 token 정보 구성
   - `sdlc.ai_jobs` 테이블에서 branch로 `job_id` 조회 (없으면 NULL로 INSERT)
   - `sdlc.ai_model_runs`에 아래 컬럼 INSERT:
     - `job_id`: ai_jobs.id (nullable)
     - `model_name`: "claude-sonnet-4-6"
     - `provider`: "anthropic"
     - `total_tokens`: consume_tokens 값
     - `cache_read_tokens`: cache_tokens 값
     - `started_at`: created_at (task JSON의 created_at)
     - `completed_at`: NOW()
     - `raw_usage`: JSON 문자열 (duration/consume/cache 원본)
   - PostgreSQL 연결 실패(psycopg2 미설치, 서버 다운 등) → stderr 경고 후 exit 0
   - `SDLC_DB_CONNECTION` 환경변수로 연결 문자열 주입 (기본값: migrate_tasks_to_postgres.py와 동일)

2. **`/pr` SKILL.md 수정**
   - 4단계(cost-log 기록) 완료 직후에 5단계로 insert_model_run.py 호출 추가
   - 호출 예시:
     ```bash
     CREATED_AT=$(grep -o '"created_at": "[^"]*"' "$TASK_FILE" | grep -o '[0-9T:Z-]*' | head -1)
     python .github/scripts/insert_model_run.py \
       --branch "$(git branch --show-current)" \
       --created-at "${CREATED_AT}" 2>/dev/null || true
     ```
   - 실패해도 /pr 전체 흐름 중단 금지 (`|| true`)

## 영향 범위 (예상)

- `.github/scripts/insert_model_run.py` — 신규 파일 (Python, ~70줄)
- `.claude/skills/pr/SKILL.md` — 4단계 완료 후 호출 구문 추가 (~10줄)
- C# 코드 변경 없음

## 제약 및 주의사항

- ADR-009: PostgreSQL SDLC DB 채택 — sdlc 스키마, `SDLC_DB_CONNECTION` 환경변수 규칙 준수
- `migrate_tasks_to_postgres.py`의 psycopg2 연결 패턴과 동일한 방식 사용
- psycopg2가 로컬에 없을 수 있음 → ImportError 처리 필수
- ai_model_runs.input_tokens / output_tokens 컬럼이 존재하지만 count_tokens.py는 합계(consume)만 반환
  → `total_tokens = consume_tokens`, `input_tokens = NULL`, `output_tokens = NULL`
- 멀티 브랜치 안전: branch 컬럼으로 ai_jobs 조회하므로 브랜치별 독립 동작

## 구현 접근 방향

1. `migrate_tasks_to_postgres.py`의 `parse_conn()` / `get_conn()` 패턴을 그대로 복사하여 연결 구성
2. ai_jobs 조회: `SELECT id FROM sdlc.ai_jobs WHERE branch = %s LIMIT 1`
3. ai_model_runs INSERT: `INSERT INTO sdlc.ai_model_runs (...) VALUES (...)`
4. `/pr` SKILL.md: `### 5단계: ai_model_runs 기록 (선택)` 섹션 추가

## 검증 기준

- `/pr` 실행 후 `SELECT * FROM sdlc.ai_model_runs ORDER BY id DESC LIMIT 1;` 으로 행 확인
- PostgreSQL 미실행 상태에서 `/pr` 실행 시 cost-log.md는 정상 기록되고 경고만 출력됨
- `backfill_cost_log.py`와 같이 Python 3.9에서도 동작 (`Optional` 타입 힌트 사용)
