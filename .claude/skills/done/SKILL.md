---
name: done
schema_version: 1
description: 현재 브랜치의 BUILD_GATE를 수행한다. 미커밋 변경사항 커밋 → 빌드/포맷/테스트 검증 → push 순서로 진행한다. PR 생성은 /pr 스킬이 담당한다.
disable-model-invocation: true
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

### 1.5단계: test-gen 미실행 경고

코드 변경이 있고 test_generated가 false이면 경고를 출력한다 (중단하지 않음):

```bash
CURRENT_BRANCH=$(git branch --show-current)
TASK_FILE=$(grep -rl "\"branch\": \"${CURRENT_BRANCH}\"" AI/tasks/ 2>/dev/null | head -1)
CODE_CHANGED=$(git diff --name-only origin/main...HEAD 2>/dev/null \
  | grep -E '\.(cs|proto|csproj)$' | head -1)
TEST_GEN=$([ -n "$TASK_FILE" ] && grep -o '"test_generated":[[:space:]]*[^,}]*' "$TASK_FILE" | grep -o 'true\|false' | head -1 || echo "true")
```

CODE_CHANGED가 있고 TEST_GEN이 `false`이면:
> ⚠️ 경고: 코드 변경이 있지만 /test-gen이 실행되지 않았습니다.
>    권장 흐름: /test-gen 실행 후 /done
>    최종 PR 생성 전 /pr에서 강제 검사됩니다.

경고를 출력한 뒤 계속 진행한다. BUILD_GATE는 중단하지 않는다.

### 2단계: 빌드 검증
```bash
# git 루트 기반으로 경로를 찾아 실행 (현재 디렉토리 무관)
SLN=$(git rev-parse --show-toplevel)/PlatformA
cd "$SLN" && dotnet build PlatformA.sln --verbosity minimal
```
빌드 실패 시:
- task JSON `"status"` → `"failed"`, `"last_error"` → 오류 요약으로 Edit 도구 업데이트
- **즉시 중단**하고 오류를 출력한다. push 금지.

### 3단계: 포맷 검사
```bash
dotnet format PlatformA.sln whitespace --verify-no-changes --no-restore
dotnet format PlatformA.sln style --verify-no-changes --no-restore
```
포맷 불일치 시 **즉시 중단**한다.
수정 명령: `dotnet format PlatformA.sln whitespace --no-restore && dotnet format PlatformA.sln style --no-restore`
수정 후 재커밋 → 3단계 재실행. push 금지.

### 4단계: 테스트 검증
```bash
dotnet test PlatformA.sln -q
```
테스트 실패 시:
- task JSON `"status"` → `"failed"`, `"last_error"` → 실패 테스트명으로 Edit 도구 업데이트
- **즉시 중단**하고 실패 항목을 출력한다. push 금지.

### 4.5단계: task 상태 → "testing"

빌드·포맷·테스트 모두 통과한 직후:
```bash
# Edit 도구로 "status" 필드 값을 "testing" 으로 교체
```

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
