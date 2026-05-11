---
name: plan
description: 새 작업 계획을 수립한다. 현재 브랜치에 오픈 PR이 있으면 해당 브랜치에서 계속 작업한다. 그 외에는 무조건 main으로 이동 후 pull 받고 새 브랜치를 생성한다.
disable-model-invocation: true
allowed-tools: Bash(git *) Bash(gh *) Read Edit
---

# 새 작업 계획 수립

## 컨텍스트
- 오늘 날짜: !`date +%Y-%m-%d`
- 현재 브랜치: !`git branch --show-current`
- 현재 브랜치 PR 상태: !`export PATH="/c/Program Files/GitHub CLI:$PATH"; branch=$(git branch --show-current); if [ "$branch" = "main" ]; then echo "[]"; else gh pr list --head "$branch" --state open --json number,state,title --limit 1 2>/dev/null || echo "[]"; fi`

## 사용자 작업 설명
$ARGUMENTS

---

## 수행 순서

### 사전 검사

`$ARGUMENTS`가 비어 있으면 작업 설명을 요청하고 **중단**한다.

---

### 브랜치 결정

#### 현재 브랜치에 오픈 PR이 있는 경우 (`state: "OPEN"`)

새 브랜치를 만들지 않는다. 기존 브랜치에서 계속 작업한다:

```
ℹ️  현재 브랜치 '{브랜치명}'에 열린 PR #{번호}이 있습니다.
    해당 브랜치에서 작업을 계속 진행합니다.
```

SPRINT.md 업데이트(4단계)로 바로 이동한다.

---

#### 그 외 모든 경우 (main, PR MERGED, PR 없음)

어느 브랜치에 있건 **무조건** main으로 이동 후 최신화한다:

```bash
git checkout main
git pull origin main
```

이후 1단계로 진행한다.

---

### 1단계: PlanName 생성

사용자 설명을 분석하여 PascalCase 영문 PlanName을 생성한다.
- 규칙: 동사+명사 형태, 최대 30자 (예: AddAuthTests, FixRedisBug, ImproveMatchingFlow)

---

### 2단계: 브랜치명 결정

```
브랜치명 = 오늘날짜_PlanName
```

오늘 날짜는 컨텍스트의 `date +%Y-%m-%d` 값을 사용한다 (하드코딩 금지).

예시:
```
2026-05-12_AddAuthTests
2026-05-12_FixRedisBug
```

카운터(`_N`) 없음. 같은 날 여러 브랜치는 PlanName으로 구분된다.

---

### 3단계: 브랜치 생성 및 push

```bash
git checkout -b {브랜치명}
git push -u origin {브랜치명}
```

---

### 4단계: SPRINT.md 업데이트

`AI/SPRINT.md`를 읽어 가장 최근 스프린트의 `### 진행 중` 섹션에 태스크를 추가한다.
- 사용자 설명을 기반으로 구체적인 태스크 항목 2~5개를 `- [ ]` 형식으로 작성한다.
- 항목은 검증 가능한 단위로 쪼갠다.

```bash
git add AI/SPRINT.md
git commit -m "계획: {PlanName} 태스크 추가"
git push
```

---

### 5단계: 완료 보고

```
브랜치: {브랜치명}

SPRINT.md 추가 항목:
  - [ ] ...
  - [ ] ...

작업을 완료하면 /done 을 실행하세요.
```
