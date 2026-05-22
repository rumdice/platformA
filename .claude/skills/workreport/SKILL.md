---
name: workreport
schema_version: 1
description: 오늘 하루 작업 내용을 수집하여 AI/workreport/YYYY-MM-DD.md 리포트를 생성하고 main에 커밋한다. 일일 작업 마무리 시 실행한다.
disable-model-invocation: false
allowed-tools: Bash(git *) Bash(gh *) Bash(grep *) Bash(date *) Bash(cat *) Read Write Edit
---

# 일일 작업 리포트 생성

## 컨텍스트 수집

오늘 날짜:
!`date +%Y-%m-%d`

오늘 머지된 PR 목록:
!`export PATH="/c/Program Files/GitHub CLI:$PATH"; gh pr list --repo rumdice/platformA --state merged --limit 20 --json number,title,mergedAt,headRefName | python3 -c "
import sys, json
from datetime import date
today = date.today().isoformat()
prs = json.load(sys.stdin)
result = [p for p in prs if p['mergedAt'][:10] == today]
if not result:
    print('(오늘 머지된 PR 없음)')
else:
    for p in result:
        print(f'PR #{p[\"number\"]} | {p[\"title\"]} | {p[\"headRefName\"]}')
" 2>/dev/null || gh pr list --repo rumdice/platformA --state merged --limit 10`

오늘 완료된 task JSON:
!`python3 -c "
import json, os
from datetime import date
today = date.today().isoformat()
tasks_dir = 'AI/tasks'
if not os.path.exists(tasks_dir):
    print('(task 디렉토리 없음)')
else:
    found = []
    for f in sorted(os.listdir(tasks_dir)):
        if not f.endswith('.json'): continue
        try:
            d = json.loads(open(os.path.join(tasks_dir, f), encoding='utf-8').read())
            if d.get('completed_at', '') and d['completed_at'][:10] == today:
                found.append(f'sprint{d[\"sprint\"]} | {d[\"task\"]} | status={d[\"status\"]} | pr={d.get(\"pr_url\",\"\")}')
        except: pass
    print('\n'.join(found) if found else '(오늘 완료된 task 없음)')
" 2>/dev/null`

현재 스프린트 정보 (마지막 30줄):
!`tail -30 AI/SPRINT.md 2>/dev/null`

오늘 cost-log 항목:
!`python3 -c "
from datetime import date
today = date.today().isoformat()
with open('AI/cost-log.md', encoding='utf-8') as f:
    lines = [l.rstrip() for l in f if l.startswith('| ' + today)]
print('\n'.join(lines) if lines else '(오늘 cost-log 항목 없음)')
" 2>/dev/null`

---

## 리포트 생성 지침

위 컨텍스트를 바탕으로 오늘의 작업 리포트를 작성한다.

### 리포트 형식

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

...

---

## 종료 시점 상태

- 오픈 PR: N건 / 없음
- 스프린트 #N 상태: 진행 중 / 완료
- 특이사항: 있으면 기술, 없으면 없음
```

### 작성 원칙

- 머지된 PR이 없는 날에는 "오늘 머지된 PR이 없습니다." 한 줄로 섹션을 대체한다
- 각 작업 항목은 **무엇을 했고 왜 했는지** 모두 담는다 (What + Why)
- 기술 용어는 그대로 사용 (한글화 강요 금지)
- 작업 규모에 비례한 분량 — 간단한 날은 짧게, 복잡한 날은 길게

---

## 저장 및 커밋

리포트 생성 후 아래 순서로 처리한다.

### 1단계: 파일 저장

Write 도구로 `AI/workreport/{오늘날짜}.md`에 저장한다.
파일이 이미 존재하면 덮어쓴다.

### 2단계: 오픈 PR 수 확인

```bash
export PATH="/c/Program Files/GitHub CLI:$PATH"
gh pr list --repo rumdice/platformA --state open --json number | python3 -c "import sys,json; print(len(json.load(sys.stdin)))"
```

### 3단계: main 직접 커밋·push

작업 리포트는 문서 전용 커밋이므로 PR 없이 main에 직접 커밋한다.

```bash
git checkout main
git pull --quiet
git add AI/workreport/{오늘날짜}.md
git commit -m "docs: {오늘날짜} 작업 리포트 추가"
git push
```

push 실패(브랜치 보호) 시: 현재 작업 브랜치로 커밋 후 `git push --set-upstream origin {브랜치}` 로 대체한다.

### 4단계: 완료 보고

```
일일 리포트 저장 완료
파일: AI/workreport/{오늘날짜}.md
PR: N건 머지 / 오픈 N건
```
