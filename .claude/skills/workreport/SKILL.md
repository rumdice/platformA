---
name: workreport
schema_version: 1
description: 오늘 하루 작업 내용을 수집하여 AI/workreport/YYYY-MM-DD.md 리포트를 생성하고 main에 커밋한다. 일일 작업 마무리 시 실행한다.
disable-model-invocation: false
allowed-tools: Bash(git *) Bash(gh *) Bash(grep *) Bash(date *) Bash(cat *) Bash(tail *) Bash(mkdir *) Read Write Edit
---

# 일일 작업 리포트 생성

## 사전 컨텍스트

오늘 날짜:
!`date +%Y-%m-%d`

---

## 수행 순서

### 1단계: 데이터 수집

아래 명령들을 순서대로 실행하여 오늘의 작업 데이터를 수집한다.

**오늘 머지된 PR 목록:**
```bash
export PATH="/c/Program Files/GitHub CLI:$PATH"
TODAY=$(date +%Y-%m-%d)
gh pr list --state merged --limit 30 --json number,title,mergedAt,headRefName
```
결과에서 `mergedAt`이 오늘 날짜(`TODAY`)인 항목만 추출한다.

**오늘 완료된 sprint 파일:**
```bash
TODAY=$(date +%Y-%m-%d)
grep -rl "^completed: ${TODAY}" AI/sprints/ 2>/dev/null
grep -rl "\"completed_at\": \"${TODAY}" AI/tasks/ 2>/dev/null
```
찾은 각 파일을 Read 도구로 읽어 sprint, title, branch, status, pr 정보를 확인한다.

**오늘 DB 완료 job (Phase C):**
```bash
python .github/scripts/db_write.py --action list-active 2>/dev/null
```
status가 done인 항목에서 오늘 sprint 번호와 task를 확인한다.

**현재 오픈 PR:**
```bash
export PATH="/c/Program Files/GitHub CLI:$PATH"
gh pr list --state open --limit 10
```

---

### 2단계: 리포트 작성

수집한 데이터를 바탕으로 아래 형식의 리포트를 작성한다.

```markdown
# 작업 리포트 — YYYY-MM-DD

## 머지된 PR (N건)

| PR | 제목 | 스프린트 |
|----|------|---------|
| #N | 제목 | #N |

---

## 주요 작업 내용

### 1. 작업명 (PR #N, 스프린트 #N)

- 핵심 변경 사항을 불릿으로 서술
- 기술적 결정 및 배경 포함

---

## 종료 시점 상태

- 오픈 PR: N건 / 없음
- 스프린트 #N 상태: 진행 중 / 완료
- 특이사항: 없음
```

**작성 원칙:**
- 각 PR 항목은 **무엇을 했고 왜 했는지** 모두 담는다
- cost-log의 규모(S/M/L/XL)를 작업 설명에 반영한다
- 오늘 머지된 PR이 없으면 "오늘 머지된 PR이 없습니다." 한 줄로 섹션을 대체한다
- 기술 용어는 그대로 사용한다

---

### 3단계: 파일 저장

```bash
mkdir -p AI/workreport
TODAY=$(date +%Y-%m-%d)
```

Write 도구로 `AI/workreport/{TODAY}.md`에 저장한다. 기존 파일이 있으면 덮어쓴다.

---

### 4단계: main 커밋·push

```bash
TODAY=$(date +%Y-%m-%d)
git checkout main
git pull --quiet
git add AI/workreport/${TODAY}.md
git commit -m "docs: ${TODAY} 작업 리포트 추가"
git push
```

push 실패 시 오류 메시지를 출력하고 사용자에게 알린다.

---

### 5단계: 완료 보고

```
일일 리포트 저장 완료
파일: AI/workreport/{TODAY}.md
오픈 PR: N건
```
