---
name: pr
schema_version: 1
description: 브랜치 push 이후 PR을 생성하고 SPRINT.md 완료 체크, task JSON 상태 갱신, cost-log 기록을 수행한다. /done 이후에 실행한다. PR_SUMMARY 단계를 담당한다.
disable-model-invocation: true
allowed-tools: Bash(git *) Bash(gh *) Bash(grep *) Read Edit
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

**검사 1 — task JSON 존재 여부**
```bash
CURRENT_BRANCH=$(git branch --show-current)
TASK_FILE=$(grep -rl "\"branch\": \"${CURRENT_BRANCH}\"" AI/tasks/ 2>/dev/null | head -1)
```
TASK_FILE이 없으면 경고하고 계속한다:
> ⚠️ task JSON이 없습니다. `/plan`으로 시작된 작업이 아니면 게이트 검사를 건너뜁니다.

이하 검사 2~5는 TASK_FILE이 있을 때만 실행한다.

**검사 2 — 코드 변경 여부 판별**
```bash
CODE_CHANGED=$(git diff --name-only origin/main...HEAD 2>/dev/null \
  | grep -E '\.(cs|proto|csproj)$' | head -1)
```
CODE_CHANGED가 비어 있으면 검사 3·4를 건너뛴다 (문서/스킬만 변경).

**검사 3 — 테스트 생성 여부 (코드 변경 시 필수)**
```bash
TEST_GEN=$(grep -o '"test_generated":[[:space:]]*[^,}]*' "$TASK_FILE" | grep -o 'true\|false' | head -1)
```
CODE_CHANGED가 있고 TEST_GEN이 `false`이면 **중단**한다:
> ❌ /pr 중단: 코드 변경이 있지만 /test-gen이 실행되지 않았습니다.
>    먼저 /test-gen을 실행하거나, 테스트가 불필요한 경우 task JSON에서 test_generated를 true로 수정하세요.

**검사 4 — 고위험 조건 시 리뷰 완료 여부**
```bash
REVIEW_DONE=$(grep -o '"review_completed":[[:space:]]*[^,}]*' "$TASK_FILE" | grep -o 'true\|false' | head -1)

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
IMPACT_NULL=$(grep -o '"impact":[[:space:]]*null' "$TASK_FILE" | head -1)
```
CODE_CHANGED가 있고 IMPACT_NULL이 있으면 **중단**한다:
> ❌ /pr 중단: 코드 변경이 있지만 /impact가 실행되지 않았습니다.
>    먼저 /impact를 실행하거나, task JSON의 impact 필드를 수동으로 채운 뒤 /pr을 재실행하세요.

**검사 6 — requirement 미실행 차단**
```bash
REQ_COUNT=$(grep -A5 '"name": "requirement"' "${TASK_FILE}" 2>/dev/null | grep -c '"status": "done"' || echo "0")
[ "${REQ_COUNT:-0}" -gt 0 ] && REQUIREMENT_DONE="true" || REQUIREMENT_DONE="false"
```
REQUIREMENT_DONE이 `false`이면 **중단**한다 (CODE_CHANGED 여부와 무관하게 항상 검사):
> ❌ /pr 중단: /requirement가 실행되지 않았습니다.
>    먼저 /requirement를 실행하거나, task JSON의 steps[]에 requirement 단계를 수동으로 추가하세요.

**검사 7 — ADR 미생성 차단 (adr_required)**
```bash
ADR_REQUIRED=$(grep -o '"adr_required":[[:space:]]*[^,}]*' "$TASK_FILE" | grep -o 'true\|false' | head -1)
```
ADR_REQUIRED가 `true`이면 **중단**한다:
> ❌ /pr 중단: DESIGN_REVIEW에서 신규 ADR이 필요하다고 판정되었지만 아직 생성되지 않았습니다.
>
>    1. /adr {결정 주제}  — ADR 파일 생성
>    2. task JSON에서 adr_required를 false로 수정
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

---

### 4단계: cost-log.md 기록

**규모 자동 계산** — main 대비 변경 파일 수로 S/M/L/XL 결정:
```bash
FILE_COUNT=$(git diff --name-only origin/main...HEAD 2>/dev/null | grep -v '^$' | wc -l)
if   [ "$FILE_COUNT" -le 2  ]; then SIZE="S"
elif [ "$FILE_COUNT" -le 10 ]; then SIZE="M"
elif [ "$FILE_COUNT" -le 30 ]; then SIZE="L"
else SIZE="XL"
fi

SPRINT_NUM=$(grep -o '"sprint":[[:space:]]*[0-9]*' "${TASK_FILE}" 2>/dev/null | grep -o '[0-9]*' | head -1 || grep -c "^## 스프린트 #" AI/SPRINT.md 2>/dev/null || echo "0")
PLAN_NAME=$(git branch --show-current | sed 's/^[0-9-]*_//')
TODAY=$(date +%Y-%m-%d)
```

**duration_sec / consume_tokens / cache_tokens 자동 계산** (Python 단일 호출):
```bash
CREATED_AT=$(grep -o '"created_at": "[^"]*"' "$TASK_FILE" | grep -o '[0-9T:Z-]*' | head -1)
TOKENS_RAW=$(python3 .github/scripts/count_tokens.py "${CREATED_AT}" 2>/dev/null \
  || python .github/scripts/count_tokens.py "${CREATED_AT}" 2>/dev/null || echo "")
DURATION=$(echo "$TOKENS_RAW" | grep "^duration_sec=" | cut -d= -f2)
CONSUME_TOKENS=$(echo "$TOKENS_RAW" | grep "^consume_tokens=" | cut -d= -f2)
CACHE_TOKENS=$(echo "$TOKENS_RAW" | grep "^cache_tokens=" | cut -d= -f2)
DURATION=${DURATION:-null}
CONSUME_TOKENS=${CONSUME_TOKENS:-null}
CACHE_TOKENS=${CACHE_TOKENS:-null}
```

task JSON에 자동 계산 결과를 기록한 뒤 cost-log 행을 추가한다:

Edit 도구를 사용하여 task JSON의 `consume_tokens`, `cache_tokens` 필드를 직접 수정한다:
- `"consume_tokens": null` → `"consume_tokens": ${CONSUME_TOKENS}`
- `"cache_tokens": null` → `"cache_tokens": ${CACHE_TOKENS}`

`AI/cost-log.md` 테이블 마지막 행에 추가 (Edit 도구):
```
| {TODAY} | #{SPRINT_NUM} | {PLAN_NAME} | claude-sonnet-4-6 | {SIZE} | {DURATION} | {CONSUME_TOKENS} | {CACHE_TOKENS} | {변경 내용 한 줄 요약} |
```

변경 후 커밋:
```bash
git add AI/tasks/ AI/cost-log.md
git commit -m "완료: task 상태 및 비용 로그 업데이트"
git push
```

---

### 4.5단계: plan 명세 파일 archived

task 완료 시 해당 브랜치의 명세 파일(`.claude/plan/YYYY-MM-DD_NNN_PlanName.md`)을 `processed/`로 이동한다.

```bash
TODAY=$(date +%Y-%m-%d)
PLAN_NAME=$(git branch --show-current | sed 's/^[0-9-]*_//')
SPEC_FILE=$(ls .claude/plan/${TODAY}_*_${PLAN_NAME}.md 2>/dev/null | head -1)
if [ -n "$SPEC_FILE" ]; then
    mkdir -p .claude/plan/processed
    mv "$SPEC_FILE" ".claude/plan/processed/$(basename $SPEC_FILE)"
    git add .claude/plan/
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
