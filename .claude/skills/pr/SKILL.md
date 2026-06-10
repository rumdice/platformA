---
name: pr
schema_version: 1
description: 브랜치 push 이후 PR을 생성하고 SPRINT.md 완료 체크, task JSON 상태 갱신, cost-log 기록을 수행한다. /done 이후에 실행한다. PR_SUMMARY 단계를 담당한다.
disable-model-invocation: false
allowed-tools: Bash(git *) Bash(gh *) Bash(grep *) Bash(python *) Bash(python3 *) Read Edit
---

# PR 생성 및 완료 처리 (PR_SUMMARY)

## 컨텍스트
- 현재 브랜치: !`git branch --show-current`
- main 대비 커밋: !`git log origin/main..HEAD --oneline 2>/dev/null`
- main 대비 변경 파일: !`git diff --name-only origin/main...HEAD 2>/dev/null`

---

## 헬퍼: task JSON 조회

```bash
CURRENT_BRANCH=$(git branch --show-current)
TASK_FILE=$(grep -rl "\"branch\": \"${CURRENT_BRANCH}\"" AI/tasks/ 2>/dev/null | head -1)
```

---

## 수행 순서

### 사전 검사

현재 브랜치가 `main`이면 즉시 중단한다:
> "main 브랜치에서는 /pr을 실행할 수 없습니다."

이미 PR이 존재하는지 확인한다:
```bash
export PATH="/c/Program Files/GitHub CLI:$PATH"
branch=$(git branch --show-current)
gh pr list --head "$branch" --state open --json number,url --limit 1
```
PR이 이미 존재하면 URL을 출력하고 중단한다:
> "이 브랜치에 이미 PR #{번호}이 존재합니다: {URL}"

---

### 게이트 검사 (사전 검사 통과 후 실행)

아래 검사를 순서대로 실행한다. **중단** 표시가 있으면 이후 단계를 실행하지 않는다.

**검사 0 — PostgreSQL 게이트 조회 (필수 — Phase C)**
```bash
CURRENT_BRANCH=$(git branch --show-current)
DB_GATES=$(python .github/scripts/db_write.py --action get-gates --branch "${CURRENT_BRANCH}" 2>&1)
if [ $? -ne 0 ] || [ -z "$DB_GATES" ]; then
  echo "❌ DB 게이트 조회 실패. PostgreSQL 연결을 확인하세요."
  exit 1
fi
DB_TEST_GEN=$(echo "$DB_GATES" | grep "^test_generated=" | cut -d= -f2)
DB_REVIEW=$(echo "$DB_GATES" | grep "^review_completed=" | cut -d= -f2)
DB_IMPACT=$(echo "$DB_GATES" | grep "^impact_done=" | cut -d= -f2)
DB_REQ=$(echo "$DB_GATES" | grep "^requirement_done=" | cut -d= -f2)
DB_ADR=$(echo "$DB_GATES" | grep "^adr_required=" | cut -d= -f2)
```

**검사 1 — task JSON 존재 여부 (참고용 — Phase C에서는 없을 수 있음)**
```bash
TASK_FILE=$(grep -rl "\"branch\": \"${CURRENT_BRANCH}\"" AI/tasks/ 2>/dev/null | head -1)
# Phase C: TASK_FILE 없음은 정상 — 모든 게이트 값은 DB에서 읽는다
```

**검사 2 — 코드 변경 여부 판별**
```bash
CODE_CHANGED=$(git diff --name-only origin/main...HEAD 2>/dev/null \
  | grep -E '\.(cs|proto|csproj)$' | head -1)
```
CODE_CHANGED가 비어 있으면 검사 3·4를 건너뛴다 (문서/스킬만 변경).

**검사 3 — 테스트 생성 여부 (코드 변경 시 필수)**
```bash
# Phase C: DB에서만 읽음
TEST_GEN="${DB_TEST_GEN}"
```
CODE_CHANGED가 있고 TEST_GEN이 `false`이면 **중단**한다:
> ❌ /pr 중단: 코드 변경이 있지만 /test-gen이 실행되지 않았습니다.
>    /test-gen 실행 후 /pr을 재실행하세요.

**검사 4 — 고위험 조건 시 리뷰 완료 여부**
```bash
# Phase C: DB에서만 읽음
REVIEW_DONE="${DB_REVIEW}"

HIGH_RISK_FILES=$(git diff --name-only origin/main...HEAD 2>/dev/null \
  | grep -E 'PlatformA\.Library/|Migrations/|DbContext|Entities/|Auth|Token|Jwt|Redis.*Lock|LockManager' \
  | head -5)

CHANGED_COUNT=$(git diff --name-only origin/main...HEAD 2>/dev/null | grep -v '^$' | wc -l)
```
아래 조건 중 하나라도 해당하고 REVIEW_DONE이 `false`이면 **중단**한다:
- HIGH_RISK_FILES가 비어 있지 않음
- CHANGED_COUNT가 10 초과

> ❌ /pr 중단: 고위험 변경(핵심 라이브러리·DB·인증·Redis)이 있지만 /review가 실행되지 않았습니다.
>    먼저 /review를 실행하세요.

**검사 5 — impact 미실행 차단 (코드 변경 시)**
```bash
# Phase C: DB에서만 읽음
[ "${DB_IMPACT}" = "true" ] && IMPACT_NULL="" || IMPACT_NULL="null"
```
CODE_CHANGED가 있고 IMPACT_NULL이 있으면 **중단**한다:
> ❌ /pr 중단: 코드 변경이 있지만 /impact가 실행되지 않았습니다.
>    /impact 실행 후 /pr을 재실행하세요.

**검사 6 — requirement 미실행 차단**
```bash
# Phase C: DB에서만 읽음
REQUIREMENT_DONE="${DB_REQ}"
```
REQUIREMENT_DONE이 `false`이면 **중단**한다 (CODE_CHANGED 여부와 무관하게 항상 검사):
> ❌ /pr 중단: /requirement가 실행되지 않았습니다.
>    /requirement를 실행하세요.

**검사 7 — ADR 미생성 차단 (adr_required)**
```bash
# Phase C: DB에서만 읽음
ADR_REQUIRED="${DB_ADR}"
```
ADR_REQUIRED가 `true`이면 **중단**한다:
> ❌ /pr 중단: DESIGN_REVIEW에서 신규 ADR이 필요하다고 판정되었지만 아직 생성되지 않았습니다.
>
>    1. /adr {결정 주제}  — ADR 파일 생성
>    2. DB에서 adr_required를 false로 업데이트
>    3. /pr 재실행

---

### 1단계: SPRINT.md 완료 체크

`AI/SPRINT.md`를 읽어 이번 브랜치에서 작업한 태스크 항목의 `- [ ]`를 `- [x]`로 변경한다.
변경 후 커밋:
```bash
git add AI/SPRINT.md
git commit -m "완료: {PlanName} 태스크 체크"
git push
```

---

### 2단계: PR 생성

브랜치명에서 PlanName을 추출하고, 커밋 이력과 변경 파일을 분석하여 **한글** PR을 생성한다.

```bash
export PATH="/c/Program Files/GitHub CLI:$PATH"
gh pr create \
  --title "{한글 PR 제목}" \
  --body "$(cat <<'EOF'
## 작업 요약
{변경 내용을 2~4줄로 요약}

## 변경 파일
{변경된 주요 파일 목록}

## 테스트 결과
- 빌드: ✔ 성공
- 테스트: ✔ N개 통과

## 관련 스프린트 태스크
{이번 작업에서 완료된 SPRINT.md 항목들}

🤖 Claude Code로 생성됨
EOF
)"
```

---

### 3단계: task JSON 완료 처리

```bash
NOW=$(date -u +%Y-%m-%dT%H:%M:%SZ)
# Edit 도구로 status="done", completed_at=NOW, pr_url={PR_URL} 교체
```

task JSON 갱신 완료 후 PostgreSQL dual-write 시도 (선택 — 연결 실패 시 무시):
```bash
python .github/scripts/db_write.py \
  --action upsert-job \
  --branch "$(git branch --show-current)" \
  --status "done" 2>/dev/null || true
python .github/scripts/db_write.py \
  --action insert-step \
  --branch "$(git branch --show-current)" \
  --step-name "pr" \
  --step-status "done" \
  --step-summary "PR 생성 완료" 2>/dev/null || true
```

---

### 4단계: 비용 기록 (Phase C: DB 기반 report)

**토큰 사용량 DB 갱신** — count_tokens.py로 계산 후 ai_jobs에 저장:
```bash
# CREATED_AT: DB ai_jobs.created_at에서 조회
CREATED_AT=$(python3 -c "
import psycopg2, os
conn_str = os.environ.get('SDLC_DB_CONNECTION', 'Host=localhost;Port=5432;Database=platforma_sdlc;Username=platforma;Password=platforma_dev_password')
parts = {}
for p in conn_str.split(';'):
    if '=' in p:
        k, v = p.split('=', 1)
        parts[k.strip().lower()] = v.strip()
conn = psycopg2.connect(host=parts.get('host','localhost'), port=int(parts.get('port',5432)), dbname=parts.get('database','platforma_sdlc'), user=parts.get('username','platforma'), password=parts.get('password','platforma_dev_password'))
cur = conn.cursor()
cur.execute('SELECT created_at FROM sdlc.ai_jobs WHERE branch = %s LIMIT 1', ('$(git branch --show-current)',))
row = cur.fetchone()
print(row[0].strftime('%Y-%m-%dT%H:%M:%SZ') if row else '')
conn.close()
" 2>/dev/null)

TOKENS_RAW=$(python .github/scripts/count_tokens.py "${CREATED_AT}" 2>/dev/null || echo "")
DURATION=$(echo "$TOKENS_RAW" | grep "^duration_sec=" | cut -d= -f2)
CONSUME_TOKENS=$(echo "$TOKENS_RAW" | grep "^consume_tokens=" | cut -d= -f2)
CACHE_TOKENS=$(echo "$TOKENS_RAW" | grep "^cache_tokens=" | cut -d= -f2)
```

CONSUME_TOKENS, CACHE_TOKENS가 있으면 ai_jobs를 업데이트한다 (직접 SQL 또는 db_write.py 확장).

**Phase C: AI/cost-log.md append 없음** — DB 기반 report를 자동 생성한다:
```bash
mkdir -p AI/reports
python .github/scripts/generate_cost_log_from_db.py \
  --output AI/reports/generated-cost-log-from-db.md 2>/dev/null || true
```

변경 후 커밋:
```bash
git add AI/tasks/ AI/reports/
git commit -m "완료: task 상태 및 비용 로그 업데이트"
git push
```

---

### 4.2단계: ai_model_runs 기록 (선택)

4단계에서 계산한 `CREATED_AT`을 사용하여 `sdlc.ai_model_runs`에 토큰 사용량을 기록한다.
PostgreSQL 미실행이거나 psycopg2가 없어도 `|| true`로 흐름을 차단하지 않는다.

```bash
CREATED_AT=$(grep -o '"created_at": "[^"]*"' "$TASK_FILE" | grep -o '[0-9T:Z-]*' | head -1)
python .github/scripts/insert_model_run.py \
  --branch "$(git branch --show-current)" \
  --created-at "${CREATED_AT}" 2>/dev/null || true
```

---

### 4.5단계: plan 명세 파일 archived

task 완료 시 해당 브랜치의 명세 파일(`.claude/plan/YYYY-MM-DD_NNN_PlanName.md`)을 `processed/`로 이동한다.

```bash
REPO_ROOT=$(git rev-parse --show-toplevel)
PLAN_NAME=$(git branch --show-current | sed 's/^[0-9-]*_//')
SPEC_FILE=$(ls "${REPO_ROOT}/.claude/plan/"*_${PLAN_NAME}.md 2>/dev/null | head -1)
if [ -n "$SPEC_FILE" ]; then
    mkdir -p "${REPO_ROOT}/.claude/plan/processed"
    mv "$SPEC_FILE" "${REPO_ROOT}/.claude/plan/processed/$(basename $SPEC_FILE)"
    git add "${REPO_ROOT}/.claude/plan/"
    git commit -m "chore: ${PLAN_NAME} 명세 파일 archived"
    git push
fi
```

명세 파일이 없으면 이 단계를 건너뛴다 (경고 없음 — `/requirement`를 실행하지 않은 작업일 수 있음).

---

### 5단계: 완료 보고

```
PR: {PR URL}
브랜치: {브랜치명}
상태: GitHub에서 PR을 검토 후 main으로 머지하세요.
```
