---
name: pr
schema_version: 1
description: 브랜치 push 이후 PR을 생성하고 SPRINT.md 완료 체크, task JSON 상태 갱신, cost-log 기록을 수행한다. /done 이후에 실행한다. PR_SUMMARY 단계를 담당한다.
disable-model-invocation: true
allowed-tools: Bash(git *) Bash(gh *) Read Edit
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

SPRINT_NUM=$(grep -c "^## 스프린트 #" AI/SPRINT.md 2>/dev/null || echo "0")
PLAN_NAME=$(git branch --show-current | sed 's/^[0-9-]*_//')
TODAY=$(date +%Y-%m-%d)
```

`AI/cost-log.md` 테이블 마지막 행에 추가 (Edit 도구):
```
| {TODAY} | #{SPRINT_NUM} | {PLAN_NAME} | claude-sonnet-4-6 | {SIZE} | {변경 내용 한 줄 요약} |
```

변경 후 커밋:
```bash
git add AI/tasks/ AI/cost-log.md
git commit -m "완료: task 상태 및 비용 로그 업데이트"
git push
```

---

### 5단계: 완료 보고

```
PR: {PR URL}
브랜치: {브랜치명}
상태: GitHub에서 PR을 검토 후 main으로 머지하세요.
```
