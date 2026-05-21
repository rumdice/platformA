---
name: start
schema_version: 1
description: 코드 작성 시작을 선언한다. task 상태를 analyzing→coding으로 전환하고 명세 파일 기반 작업 지시서를 출력한다. /plan 완료 직후 코딩 시작 전 실행한다. CODE_FIX 단계 진입점.
allowed-tools: Bash(git *) Bash(ls *) Read Edit
---

# 코딩 시작 선언 (CODE_FIX 진입)

## 컨텍스트
- 현재 브랜치: !`git branch --show-current`
- 오늘 명세 파일: !`ls .claude/plan/$(date +%Y-%m-%d)_*.md 2>/dev/null | sort | tail -1 || echo "(없음)"`
- 전체 명세 파일: !`ls .claude/plan/processed/ 2>/dev/null | sort -r | head -5 || echo "(없음)"`

---

## 수행 순서

### 1단계: task JSON 상태 → "coding"

```bash
CURRENT_BRANCH=$(git branch --show-current)
TASK_FILE=$(grep -rl "\"branch\": \"${CURRENT_BRANCH}\"" AI/tasks/ 2>/dev/null | head -1)
```

TASK_FILE이 있으면 Edit 도구로 `"status"` 필드를 `"coding"`으로 교체한다.
없으면 해당 단계를 건너뛴다.

---

### 2단계: 명세 파일 탐색

아래 순서로 이번 브랜치에 해당하는 명세 파일을 찾는다:

1. **오늘 날짜 명세** (아직 processed 이전): `.claude/plan/YYYY-MM-DD_NNN_PlanName.md`
2. **processed 명세**: `.claude/plan/processed/YYYY-MM-DD_NNN_{PlanName}.md`
   - 브랜치명의 PlanName 부분(`2026-05-21_CleanupUtilsApi` → `CleanupUtilsApi`)으로 파일명 검색

명세 파일을 찾으면 Read 도구로 읽는다.
찾지 못하면 "명세 파일 없음 — /requirement를 먼저 실행하세요." 출력 후 중단.

---

### 3단계: 작업 지시서 출력

명세 파일 내용을 바탕으로 아래 형식으로 출력한다:

```
🚀 코딩 시작 — {PlanName}

브랜치: {브랜치명}
task 상태: analyzing → coding

## 구현할 내용
{명세 파일의 "상세 요구사항" 섹션 요약}

## 변경 예정 파일
{명세 파일의 "영향 범위" 섹션}

## 완료 기준
{명세 파일의 "검증 기준" 섹션}

다음 단계:
  코딩 완료 후 /done  — 빌드·테스트 검증 및 push
  push 완료 후 /pr    — PR 생성 및 완료 처리
```
