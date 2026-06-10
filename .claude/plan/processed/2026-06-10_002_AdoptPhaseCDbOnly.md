# 요구사항 명세: AdoptPhaseCDbOnly

작성일: 2026-06-10
브랜치: 2026-06-10_AdoptPhaseCDbOnly
소스: 사용자 지시 + Docs/operations/ai-sdlc-phase-c-db-only-plan.md

---

## 요구사항 요약

AI_SDLC를 Phase C(DB 단독)로 전환한다.
task JSON 신규 쓰기·cost-log.md append·gate 검사 파일 fallback을 제거하고
PostgreSQL을 SDLC 상태의 단일 진실원으로 확립한다.

---

## 상세 요구사항

### 0. 버그 수정 (사전 조건)

**0-1. `generate_cost_log_from_db.py` 스키마 수정**
- 현재 쿼리가 `ai_model_runs.model_id`, `ai_model_runs.consume_tokens` 등 존재하지 않는 컬럼 참조
- 수정: `ai_jobs`의 집계값 (`duration_sec`, `consume_tokens`, `cache_tokens`) 사용
- model_name은 `ai_model_runs`에서 최신 행 서브쿼리로 조회
- 조건: `status='done' AND consume_tokens IS NOT NULL`
- **이미 로컬에서 수정 완료** (main push 전)

**0-2. `insert_model_run.py` em dash 수정**
- cp949 인코딩 불가 문자 `—` → `-` 교체
- **이미 로컬에서 수정 완료** (main push 전)

---

### C.1: task JSON 신규 쓰기 중단 (`/plan` SKILL.md)

**제거 대상:**
```bash
# 아래 블록 전체 제거
cat > "AI/tasks/sprint${SPRINT_NUM}_${PLAN_NAME}.json" << EOF
...
EOF
git add "AI/tasks/sprint${SPRINT_NUM}_${PLAN_NAME}.json"
git commit -m "계획[1/2]: ${PLAN_NAME} task JSON 초기화"
git push
```

**유지:**
```bash
# DB INSERT만 수행 (upsert-job) — 연결 실패 시 오류 출력 (graceful skip 아님)
python .github/scripts/db_write.py \
  --action upsert-job \
  --branch "${BRANCH}" \
  --sprint "${SPRINT_NUM}" \
  --task "${PLAN_NAME}" \
  --status "analyzing" \
  --created-at "${NOW}"
```

**기타 변경:**
- 커밋 메시지 `"계획[1/2]"` → `"계획: ${PLAN_NAME} 스프린트 초기화"` (단일 커밋으로 통합)
- `SPRINT_NUM` 계산: 파일 수 기반 → **DB SELECT MAX(sprint)+1** 방식으로 전환

```bash
SPRINT_NUM=$(python -c "
import psycopg2, os
# DB에서 max sprint 조회 (연결 실패 시 fallback: AI/tasks 파일 수)
" 2>/dev/null || ls AI/tasks/sprint*.json 2>/dev/null | grep -o '[0-9]*' | sort -n | tail -1)
SPRINT_NUM=$(( ${SPRINT_NUM:-0} + 1 ))
```

---

### C.2: cost-log.md append 중단 (`/pr` SKILL.md)

**제거 대상 (4단계 cost-log.md Edit append):**
```bash
# 이 블록 제거
Edit 도구를 사용하여 AI/cost-log.md 테이블 마지막 행에 추가
```

**추가:**
```bash
# /pr 완료 후 DB 기반 report 자동 생성
python .github/scripts/generate_cost_log_from_db.py \
  --output AI/reports/generated-cost-log-from-db.md 2>/dev/null || true

if [ -f AI/reports/generated-cost-log-from-db.md ]; then
  git add AI/reports/generated-cost-log-from-db.md
  git commit -m "chore: cost-log report 갱신 (DB 기반)"
  git push
fi
```

**`AI/cost-log.md`**: 신규 행 추가 없음. 기존 파일은 읽기 전용 아카이브.

---

### C.3: gate 검사 파일 fallback 제거 (`/pr`, `/done` SKILL.md)

**현재 패턴 (제거):**
```bash
if [ -n "$DB_TEST_GEN" ]; then
  TEST_GEN="$DB_TEST_GEN"
else
  TEST_GEN=$(grep -o '"test_generated":...' "$TASK_FILE" | ...)  # ← 파일 fallback 제거
fi
```

**변경 후 패턴:**
```bash
# DB 조회 필수 — 실패 시 오류 출력 후 중단
DB_GATES=$(python .github/scripts/db_write.py --action get-gates --branch "${CURRENT_BRANCH}" 2>&1)
if [ $? -ne 0 ] || [ -z "$DB_GATES" ]; then
  echo "❌ DB 게이트 조회 실패. PostgreSQL 연결을 확인하세요."
  exit 1
fi
TEST_GEN=$(echo "$DB_GATES" | grep "^test_generated=" | cut -d= -f2)
REVIEW_DONE=$(echo "$DB_GATES" | grep "^review_completed=" | cut -d= -f2)
IMPACT_DONE=$(echo "$DB_GATES" | grep "^impact_done=" | cut -d= -f2)
```

> **주의**: 이 변경 이후 로컬 DB 없으면 `/pr`, `/done` 실행 불가.
> PostgreSQL 연결이 SDLC 워크플로의 필수 조건이 된다.

---

### C.4: check_sdlc_consistency.py Phase C 상태 처리

Phase C 전환 후 신규 스프린트는 `AI/tasks/*.json`이 없다.
→ `missing_in_files` 카운트가 증가하지만 이는 정상.

**수정 내용:**
- `missing_in_files` 항목 중 DB status='done' (머지 완료)인 것은 `files_archived` 분류
- `--strict`에서 `files_archived`는 critical 대상 제외

---

### C.5: Phase C 문서 최종 확정

`Docs/operations/ai-sdlc-phase-c-db-only-plan.md`:
- 조건 4, 5 ✅ 완료 처리
- Phase C 선언 날짜 기록
- C.1~C.3 완료 처리

---

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|------|---------|
| `.claude/skills/plan/SKILL.md` | C.1: task JSON 생성 제거, DB sprint 카운터 |
| `.claude/skills/pr/SKILL.md` | C.2: cost-log append 제거, C.3: fallback 제거 |
| `.claude/skills/done/SKILL.md` | C.3: gate 파일 fallback 제거 |
| `.github/scripts/generate_cost_log_from_db.py` | 0-1: 스키마 수정 (이미 완료) |
| `.github/scripts/insert_model_run.py` | 0-2: em dash 수정 (이미 완료) |
| `.github/scripts/check_sdlc_consistency.py` | C.4: files_archived 분류 추가 |
| `Docs/operations/ai-sdlc-phase-c-db-only-plan.md` | C.5: 최종 확정 |
| `AI/sprints/sprint-052.md` | 스프린트 상세 |

C# 코드 변경 없음.

---

## 제약 및 주의사항

- 기존 `AI/tasks/*.json` 파일 삭제 금지 — 읽기 전용 아카이브로 보존
- `AI/cost-log.md` 직접 편집 금지 — 마지막 행(Sprint #51)이 수동 기록 마지막 행
- C.3 변경 후 이 브랜치 자체의 `/done`, `/pr` 실행 시 DB 연결 필수
- `/plan` SKILL.md에서 task JSON 생성을 제거하므로 이 스프린트가 **마지막으로 JSON을 직접 생성하는 스프린트**

---

## 구현 접근 방향

1. **버그 수정 먼저** (0-1, 0-2) — 이미 완료, 커밋만 하면 됨
2. **SKILL.md 순서**: plan → pr → done 순으로 수정
   - plan 수정 후 테스트: DB에서 sprint 번호 조회 확인
   - pr 수정 후 테스트: generate_cost_log 출력 확인
   - done 수정 후 테스트: DB get-gates 응답 확인
3. **일관성 검사**: Phase C 전환 후 `check_sdlc_consistency.py` 재실행

---

## 검증 기준

- [ ] `generate_cost_log_from_db.py --dry-run` 오류 없이 출력
- [ ] `insert_model_run.py` cp949 오류 없음
- [ ] `/plan` SKILL.md에 `AI/tasks/sprint*.json` 생성 코드 없음
- [ ] `/pr` SKILL.md에 `AI/cost-log.md` Edit 코드 없음
- [ ] `get-gates` DB 응답 없으면 `/pr`, `/done`이 명시적 오류로 중단
- [ ] `check_sdlc_consistency.py --strict` exit 0 유지
- [ ] `Docs/operations/ai-sdlc-phase-c-db-only-plan.md` Phase C 완료 선언

---

## DESIGN_REVIEW 결과

| ADR | 관련 여부 | 충돌/참고 사항 |
|-----|---------|--------------|
| ADR-007: Protobuf | 없음 | - |
| ADR-기타 인프라 ADR | 없음 | 기존 PostgreSQL 인프라 사용 (신규 없음) |

판정: ✅ 기존 ADR 준수 — 신규 인프라 없음, 기존 PostgreSQL 운영 방식 변경만
