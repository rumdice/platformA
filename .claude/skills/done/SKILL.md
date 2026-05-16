---
name: done
description: 현재 브랜치의 작업을 완료 처리한다. 미커밋 변경사항 커밋 → 빌드/테스트 검증 → 한글 PR 생성 → SPRINT.md 완료 체크 순서로 진행한다.
disable-model-invocation: true
allowed-tools: Bash(git *) Bash(dotnet *) Bash(gh *) Read Edit
---

# 작업 완료 처리

## 컨텍스트
- 현재 브랜치: !`git branch --show-current`
- 미커밋 변경사항: !`git status --short`
- 커밋되지 않은 파일: !`git diff --name-only; git diff --name-only --cached`
- 브랜치 커밋 이력: !`git log origin/main..HEAD --oneline 2>/dev/null || git log --oneline -10`

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

### 2단계: 빌드 검증
```bash
# git 루트 기반으로 경로를 찾아 실행 (현재 디렉토리 무관)
SLN=$(git rev-parse --show-toplevel)/PlatformA
cd "$SLN" && dotnet build PlatformA.sln -q
```
빌드 실패 시 **즉시 중단**하고 오류를 출력한다. push 금지.

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
테스트 실패 시 **즉시 중단**하고 실패 항목을 출력한다. push 금지.

### 5단계: 원격 push
빌드·포맷·테스트 모두 통과한 뒤 마커를 생성하고 push한다.
pre-push 훅이 마커를 감지하면 재검사를 건너뛴다 (이중 빌드 방지).
```bash
echo "$(date +%s)" > /tmp/.platformA_done_verified
git push
```

### 6단계: SPRINT.md 완료 체크
`AI/SPRINT.md`를 읽어 이번 브랜치에서 작업한 태스크 항목의 `- [ ]`를 `- [x]`로 변경한다.
변경 후 커밋:
```bash
git add AI/SPRINT.md
git commit -m "완료: {PlanName} 태스크 체크"
git push
```

### 7단계: PR 생성
브랜치명에서 PlanName을 추출하고, 커밋 이력과 변경 파일을 분석하여 **한글** PR을 생성한다.

Windows 로컬 bash 환경에서는 gh가 PATH에 없으므로 아래처럼 PATH를 먼저 설정한다:
```bash
export PATH="/c/Program Files/GitHub CLI:$PATH"
```

```bash
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

### 8단계: 완료 보고
```
PR: {PR URL}
브랜치: {브랜치명}
상태: GitHub에서 PR을 검토 후 main으로 머지하세요.
```
