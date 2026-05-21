---
name: done
schema_version: 1
description: 코드 변경을 커밋하고 빌드/포맷/테스트를 검증한 뒤 원격에 push한다. PR 생성은 /pr 스킬로 별도 실행한다. 반복 실행 안전 (push만 하므로 PR 중복 없음).
disable-model-invocation: true
allowed-tools: Bash(git *) Bash(dotnet *) Read Edit
---

# 작업 검증 및 push (BUILD_GATE)

## 컨텍스트
- 현재 브랜치: !`git branch --show-current`
- 미커밋 변경사항: !`git status --short`
- 커밋되지 않은 파일: !`git diff --name-only; git diff --name-only --cached`
- 브랜치 커밋 이력: !`git log origin/main..HEAD --oneline 2>/dev/null || git log --oneline -5`

---

## 헬퍼: task JSON 조회

```bash
CURRENT_BRANCH=$(git branch --show-current)
TASK_FILE=$(grep -rl "\"branch\": \"${CURRENT_BRANCH}\"" AI/tasks/ 2>/dev/null | head -1)
```

TASK_FILE이 비어 있으면 task JSON이 없는 것이므로 해당 단계를 건너뛴다.

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

### 1.5단계: task 상태 → "coding"

```bash
if [ -n "$TASK_FILE" ]; then
  # Edit 도구로 "status" 필드 값을 "coding" 으로 교체
fi
```

### 2단계: 빌드 검증
```bash
SLN=$(git rev-parse --show-toplevel)/PlatformA
cd "$SLN" && dotnet build PlatformA.sln -q
```
빌드 실패 시:
- task JSON `"status"` → `"failed"`, `"last_error"` → 오류 요약
- **즉시 중단**. push 금지.

### 3단계: 포맷 검사
```bash
dotnet format PlatformA.sln whitespace --verify-no-changes --no-restore
dotnet format PlatformA.sln style --verify-no-changes --no-restore
```
포맷 불일치 시 **즉시 중단**.
수정 명령: `dotnet format PlatformA.sln whitespace --no-restore && dotnet format PlatformA.sln style --no-restore`
수정 후 재커밋 → 3단계 재실행. push 금지.

### 4단계: 테스트 검증
```bash
dotnet test PlatformA.sln -q
```
테스트 실패 시:
- task JSON `"status"` → `"failed"`, `"last_error"` → 실패 테스트명
- **즉시 중단**. push 금지.

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
✔ 빌드/테스트 통과 — 브랜치 push 완료

브랜치: {브랜치명}

다음 단계:
  /pr  — PR 생성 + SPRINT 체크 + 비용 로그 기록
```
