---
name: plan
schema_version: 1
description: 새 작업 계획을 수립한다. 사용자 설명을 분석하여 PlanName을 결정하고, 브랜치 생성 + task JSON 커밋 + SPRINT 등록까지 수행한다. 워크플로우 Stage 1 진입점.
allowed-tools: Bash(git *) Bash(gh *) Bash(grep *) Bash(mkdir *) Bash(python3 *) Read Edit Write
---

# 새 작업 계획 수립 (Stage 1)

## 컨텍스트
- 오늘 날짜: !`date +%Y-%m-%d`
- 현재 브랜치: !`git branch --show-current`
- 현재 브랜치 PR 상태: !`export PATH="$PATH:/c/Program Files/GitHub CLI"; b=$(git branch --show-current); if [ -n "$b" ] && [ "$b" != "main" ]; then gh pr list --head "$b" --state open --json number,state,title --limit 1 2>/dev/null || echo "[]"; else echo "[]"; fi`

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

sprint-NNN.md 태스크 추가(4단계)로 바로 이동한다.

---

#### 그 외 모든 경우 (main, PR MERGED, PR 없음)

어느 브랜치에 있건 **무조건** main으로 이동 후 최신화한다:

```bash
git checkout main
git pull origin main
```

이후 1단계로 진행한다.

---

### 1단계: 사용자 설명 분석 및 계획 요약

사용자 설명(`$ARGUMENTS`)을 분석하여 아래 항목을 결정·출력한다:

- **작업 목적**: 무엇을 달성하려는가? (1문장)
- **예상 변경 범위**: 어떤 서비스/모듈/파일이 영향받을 것인가?
- **구현 방향**: 핵심 접근 방식 (1~3줄, 상세 설계는 /requirement에서)
- **PlanName**: 아래 규칙으로 생성

**PlanName 규칙**: PascalCase, 동사+명사 형태, 최대 30자  
예: `AddRedisRateLimit`, `FixAuthTokenExpiry`, `UpgradeEfCore`

---

### 2단계: 브랜치명 후보 결정

```
브랜치명 후보 = 오늘날짜_PlanName
```

오늘 날짜는 컨텍스트의 `date +%Y-%m-%d` 값을 사용한다 (하드코딩 금지).
이 단계에서는 후보만 결정한다 — 충돌 감지 및 확정은 3단계에서 수행한다.

---

### 2.5단계: 스프린트 번호 발급

DB `sdlc.sprint_seq`에서 원자적으로 번호를 발급받는다.
MAX(sprint)+1 방식의 TOCTOU 레이스를 제거한다.
**DB 연결 실패 시 중단** — Phase C에서 DB는 필수 조건이다.

**원칙: `/plan` 1회 = 새 스프린트 항목 1개 신설.** 기존 스프린트에 태스크를 추가하지 않는다.

```bash
# sdlc.sprint_seq nextval — 동시 호출해도 번호 충돌 없음
SPRINT_NUM=$(python .github/scripts/db_write.py --action get-sprint-num 2>/dev/null)
if [ -z "$SPRINT_NUM" ]; then
    echo "❌ DB에서 스프린트 번호 발급 실패. PostgreSQL 연결을 확인하세요." >&2
    exit 1
fi
```

이 `SPRINT_NUM`을 3단계(충돌 suffix), 3.5단계(DB 기록), 4단계(sprint-NNN.md 생성) 전부에서 동일하게 사용한다.
**재계산 금지.**

---

### 3단계: 브랜치명 확정 + 생성 + push

원격에 동일한 이름의 브랜치가 이미 존재하면 스프린트 번호를 suffix로 추가하여 충돌을 방지한다.
같은 날 같은 PlanName으로 두 개발자가 동시에 작업할 때 자동으로 구분된다.

```bash
BRANCH_NAME="${TODAY}_${PLAN_NAME}"

# 브랜치명 충돌 감지 — 원격에 동일 이름이 있으면 _S{NNN} suffix 추가
REMOTE_EXISTS=$(git ls-remote --heads origin "${BRANCH_NAME}" 2>/dev/null | wc -l | tr -d ' ')
if [ "${REMOTE_EXISTS:-0}" -gt "0" ]; then
    BRANCH_NAME="${BRANCH_NAME}_S${SPRINT_NUM}"
    echo "⚠️ 브랜치명 충돌 감지 — 스프린트 번호로 구분합니다: ${BRANCH_NAME}"
fi

git checkout -b "${BRANCH_NAME}"
git push -u origin "${BRANCH_NAME}"
```

---

### 3.5단계: DB 초기화 (Phase C: task JSON 없음)

task JSON 파일은 생성하지 않는다. DB `sdlc.ai_jobs`에만 기록한다.

```bash
# Phase C: task JSON 파일 생성 없음 — DB에만 기록
PLAN_NAME="{PlanName}"
BRANCH="{브랜치명}"
NOW=$(date -u +%Y-%m-%dT%H:%M:%SZ)

# DB upsert-job (필수 — 실패 시 오류 출력 후 중단)
# --owner: git config user.name 자동 감지 (db_write.py 내부에서 처리)
python .github/scripts/db_write.py \
  --action upsert-job \
  --branch "${BRANCH}" \
  --sprint "${SPRINT_NUM}" \
  --task "${PLAN_NAME}" \
  --status "analyzing" \
  --created-at "${NOW}"
```

---

### 4단계: sprint-NNN.md 생성 및 커밋

**Case A — 새 브랜치 (오픈 PR 없음)**:

`AI/sprints/sprint-{NNN}.md` 파일을 새로 생성한다 (NNN = 3자리 0-padding SPRINT_NUM).  
파일은 반드시 **YAML 프론트매터**로 시작해야 한다:

```markdown
---
sprint: {NNN 숫자}
title: {한글 제목 또는 PlanName (2~5단어 요약)}
branch: {브랜치명}
date: {오늘날짜}
status: in-progress
---

# Sprint #{NNN} — {제목}

## 목표
{작업 목적 1문장}

## 태스크
- [ ] {태스크 1}
- [ ] {태스크 2}
...

## 배경
{사용자 설명 요약}

## 참조
- DB job: `sdlc.ai_jobs.branch = {브랜치명}`
```

**Case B — 기존 브랜치 (오픈 PR 존재)**: sprint-NNN.md 파일에 태스크 항목을 추가한다.

```bash
NNN=$(printf "%03d" "${SPRINT_NUM}")
git add AI/sprints/sprint-${NNN}.md
git commit -m "계획: ${PLAN_NAME} 스프린트 등록"
git push
```

---

### 5단계: 완료 보고

```
✅ /plan 완료 — Stage 1: 브랜치·DB 초기화

브랜치: {브랜치명}
스프린트: #{SPRINT_NUM}
DB job: sdlc.ai_jobs.branch = {브랜치명} (task JSON 없음 — Phase C)

작업 요약:
  목적: {작업 목적}
  범위: {예상 변경 범위}
  방향: {구현 방향}

sprint-{NNN}.md 태스크:
  - [ ] ...

다음 단계:
  /requirement  — 요구사항 상세 분석 + 명세 파일 생성 (Stage 2, 권장)
  /impact       — 영향 범위 분석 (코드 수정 전 실행 권장)
  /start        — 코딩 시작 선언
```
