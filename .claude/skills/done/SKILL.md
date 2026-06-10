---
name: done
schema_version: 1
description: 현재 브랜치의 BUILD_GATE를 수행한다. 미커밋 변경사항 커밋 → 빌드/포맷/테스트 검증 → push 순서로 진행한다. PR 생성은 /pr 스킬이 담당한다.
disable-model-invocation: false
allowed-tools: Bash(git *) Bash(dotnet *) Bash(grep *) Read Edit
---

# 작업 완료 처리

## 컨텍스트
- 현재 브랜치: !`git branch --show-current`
- 미커밋 변경사항: !`git status --short`
- 커밋되지 않은 파일: !`git diff --name-only; git diff --name-only --cached`
- 브랜치 커밋 이력: !`git log origin/main..HEAD --oneline 2>/dev/null || git log --oneline -10`

---

## 헬퍼: task JSON 상태 업데이트

아래 bash 스니펫을 각 전환 시점에 사용한다. `NEW_STATUS`와 추가 필드를 상황에 맞게 교체한다.

```bash
CURRENT_BRANCH=$(git branch --show-current)
TASK_FILE=$(grep -rl "\"branch\": \"${CURRENT_BRANCH}\"" AI/tasks/ 2>/dev/null | head -1)
```

TASK_FILE이 비어 있으면 task JSON이 없는 것이다. 이 경우 **즉시 중단**하고 아래 메시지를 출력한다:

> ❌ task JSON을 찾을 수 없습니다.
>    SDLC 워크플로우가 누락되었습니다. 아래 순서로 먼저 진행하세요:
>    1. `/requirement` — 요구사항 명세 생성
>    2. `/plan PlanName` — 작업 브랜치 생성 + task JSON 초기화
>    CLAUDE.md "절대 하지 말 것" 참조.

단, `git diff --name-only origin/main...HEAD`에 `.cs`/`.proto`/`.csproj` 변경이 없고 문서·설정만 변경된 경우에는 경고 후 계속 진행한다.

---

## 수행 순서

### 사전 검사
현재 브랜치가 `main`이면 즉시 중단한다:
> "main 브랜치에서는 /done을 실행할 수 없습니다. /plan으로 작업 브랜치를 먼저 생성하세요."

### 1단계: 미커밋 변경사항 처리
미커밋 변경사항이 있으면:
- 변경 내용을 분석하여 **한글** 커밋 메시지를 작성한다.
- 커밋 메시지 형식: `타입: 한글 설명` (타입: feat / fix / test / docs / chore / refactor)
- 예: `feat: Auth API 통합 테스트 추가`, `fix: Redis 분산 락 해제 누락 수정`

```bash
git add -A
git commit -m "{한글 커밋 메시지}"
```

### 1.5단계: SDLC gate 사전 검사 (코드 변경 시 필수)

gate-check와 동일한 3가지 조건을 push 전에 로컬에서 먼저 검사한다.
CI에서 막히기 전에 차단하여 재push 비용을 줄이는 것이 목적이다.

```bash
CURRENT_BRANCH=$(git branch --show-current)
CODE_CHANGED=$(git diff --name-only origin/main...HEAD 2>/dev/null \
  | grep -E '\.(cs|proto|csproj)$' | head -1)
```

CODE_CHANGED가 없으면 이 단계를 건너뛴다 (문서·스킬만 변경).

CODE_CHANGED가 있는 경우 **DB에서 게이트 값을 조회**한다 (Phase C: 파일 fallback 없음).

```bash
# Phase C: DB 게이트 조회 필수 — 실패 시 중단
DB_GATES=$(python .github/scripts/db_write.py --action get-gates --branch "${CURRENT_BRANCH}" 2>&1)
if [ $? -ne 0 ] || [ -z "$DB_GATES" ]; then
    echo "❌ DB 게이트 조회 실패. PostgreSQL 연결을 확인하세요."
    exit 1
fi
TEST_GEN=$(echo "$DB_GATES" | grep "^test_generated=" | cut -d= -f2)
IMPACT_DONE=$(echo "$DB_GATES" | grep "^impact_done=" | cut -d= -f2)
REVIEW_DONE=$(echo "$DB_GATES" | grep "^review_completed=" | cut -d= -f2)
```

아래 3가지를 순서대로 검사한다. 하나라도 미충족이면 **즉시 중단**하고 push를 금지한다.

**검사 A — /test-gen 완료 여부**

TEST_GEN이 `false`이면 **즉시 중단**:
> ❌ /done 중단: /test-gen이 실행되지 않았습니다.
>    /test-gen 실행 후 /done을 재실행하세요.
>    테스트가 불필요한 경우: `db_write.py --action upsert-job --test-generated` 후 재실행.

**검사 B — /impact 완료 여부**

IMPACT_DONE이 `false`이면 **즉시 중단**:
> ❌ /done 중단: /impact가 실행되지 않았습니다.
>    /impact 실행 후 /done을 재실행하세요.

**검사 C — /review 완료 여부**

REVIEW_DONE이 `false`이면 **즉시 중단**:
> ❌ /done 중단: /review가 실행되지 않았습니다.
>    /review 실행 후 /done을 재실행하세요.

### 2단계: 빌드 검증
```bash
# git 루트 기반으로 경로를 찾아 실행 (현재 디렉토리 무관)
SLN=$(git rev-parse --show-toplevel)/PlatformA
cd "$SLN" && dotnet build PlatformA.sln --verbosity minimal
```
빌드 실패 시:
```bash
python .github/scripts/db_write.py \
  --action upsert-job \
  --branch "${CURRENT_BRANCH}" \
  --status "failed" \
  --last-error "빌드 실패: {오류 요약 앞 200자}" 2>/dev/null || true
```
- **즉시 중단**하고 오류를 출력한다. push 금지.

### 3단계: 포맷 검사 및 자동 수정

먼저 검증 모드로 실행한다:
```bash
dotnet format PlatformA.sln whitespace --verify-no-changes --no-restore
FORMAT_WHITESPACE=$?
dotnet format PlatformA.sln style --verify-no-changes --no-restore
FORMAT_STYLE=$?
```

**포맷 불일치가 없으면**: 그대로 4단계로 진행한다.

**포맷 불일치가 있으면**: 자동 수정 후 재커밋한다 (수동 개입 없이 계속 진행):
```bash
# 자동 수정
dotnet format PlatformA.sln whitespace --no-restore
dotnet format PlatformA.sln style --no-restore

# 변경사항 확인
FORMAT_CHANGED=$(git diff --name-only)
if [ -n "$FORMAT_CHANGED" ]; then
  git add -A
  git commit -m "chore: auto-fix dotnet format (whitespace/style)"
fi

# 재검증 — 이번에도 실패하면 중단
dotnet format PlatformA.sln whitespace --verify-no-changes --no-restore
dotnet format PlatformA.sln style --verify-no-changes --no-restore
```

재검증 실패 시 **즉시 중단**하고 오류를 출력한다. push 금지.

### 4단계: 테스트 검증
```bash
dotnet test PlatformA.sln -q
```
테스트 실패 시:
```bash
python .github/scripts/db_write.py \
  --action upsert-job \
  --branch "${CURRENT_BRANCH}" \
  --status "failed" \
  --last-error "테스트 실패: {실패 테스트명}" 2>/dev/null || true
```
- **즉시 중단**하고 실패 항목을 출력한다. push 금지.

### 4.5단계: DB 상태 → "testing" 갱신 (Phase C: DB 단독)

빌드·포맷·테스트 모두 통과한 직후 DB를 갱신한다.
task JSON이 없어도 (Phase C 신규 스프린트) DB만으로 처리한다.

```bash
python .github/scripts/db_write.py \
  --action upsert-job \
  --branch "${CURRENT_BRANCH}" \
  --status "testing" 2>/dev/null || true
python .github/scripts/db_write.py \
  --action insert-step \
  --branch "${CURRENT_BRANCH}" \
  --step-name "done" \
  --step-status "done" \
  --step-summary "빌드: 성공, 테스트 통과, push 완료" 2>/dev/null || true
```

task JSON이 존재하는 경우 (Phase B 이전 스프린트 계속 작업):
- Edit 도구로 `"status"` → `"testing"` 교체
- `steps[]`에 done 항목 추가

### 5단계: 원격 push
마커를 생성하고 push한다.
pre-push 훅이 마커를 감지하면 재검사를 건너뛴다 (이중 빌드 방지).
```bash
echo "$(date +%s)" > /tmp/.platformA_done_verified
git push
```

### 6단계: 완료 보고
```
push 완료 — {브랜치명}
빌드: ✔ 성공 / 테스트: ✔ N개 통과

다음 단계:
  /pr  — SPRINT 완료 체크 + PR 생성 + cost-log 기록
```
