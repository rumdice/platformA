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

### 1단계: 사용자 설명 분석 및 계획 요약

사용자 설명(`$ARGUMENTS`)을 분석하여 아래 항목을 결정·출력한다:

- **작업 목적**: 무엇을 달성하려는가? (1문장)
- **예상 변경 범위**: 어떤 서비스/모듈/파일이 영향받을 것인가?
- **구현 방향**: 핵심 접근 방식 (1~3줄, 상세 설계는 /requirement에서)
- **PlanName**: 아래 규칙으로 생성

**PlanName 규칙**: PascalCase, 동사+명사 형태, 최대 30자  
예: `AddRedisRateLimit`, `FixAuthTokenExpiry`, `UpgradeEfCore`

---

### 2단계: 브랜치명 결정

```
브랜치명 = 오늘날짜_PlanName
```

오늘 날짜는 컨텍스트의 `date +%Y-%m-%d` 값을 사용한다 (하드코딩 금지).

---

### 3단계: 브랜치 생성 및 push

```bash
git checkout -b {브랜치명}
git push -u origin {브랜치명}
```

---

### 3.2단계: 스프린트 번호 확정

task JSON은 더 이상 생성하지 않는다. DB `sdlc.ai_jobs`에서 MAX(sprint)를 조회하여 번호를 결정한다.
**DB 연결 실패 시 중단** — Phase C에서 DB는 필수 조건이다.

**원칙: `/plan` 1회 = 새 스프린트 항목 1개 신설.** 기존 스프린트에 태스크를 추가하지 않는다.

```bash
# DB에서 최대 sprint 번호를 조회한다 (Phase C: 파일 카운터 제거)
SPRINT_NUM=$(python3 -c "
import psycopg2, os, sys
conn_str = os.environ.get('SDLC_DB_CONNECTION', 'Host=localhost;Port=5432;Database=platforma_sdlc;Username=platforma;Password=platforma_dev_password')
parts = {}
for p in conn_str.split(';'):
    if '=' in p:
        k, v = p.split('=', 1)
        parts[k.strip().lower()] = v.strip()
conn = psycopg2.connect(host=parts.get('host','localhost'), port=int(parts.get('port',5432)), dbname=parts.get('database','platforma_sdlc'), user=parts.get('username','platforma'), password=parts.get('password','platforma_dev_password'))
cur = conn.cursor()
cur.execute('SELECT COALESCE(MAX(sprint), 0) FROM sdlc.ai_jobs')
print(cur.fetchone()[0] + 1)
conn.close()
" 2>/dev/null)
if [ -z "$SPRINT_NUM" ]; then
    echo "❌ DB에서 스프린트 번호 조회 실패. PostgreSQL 연결을 확인하세요." >&2
    exit 1
fi
```

이 `SPRINT_NUM`을 3.5단계(DB 기록)와 4단계(SPRINT.md 헤더) 양쪽에서 동일하게 사용한다.
**재계산 금지.**

---

### 3.5단계: DB 초기화 (Phase C: task JSON 없음)

task JSON 파일은 생성하지 않는다. DB `sdlc.ai_jobs`에만 기록한다.

```bash
# Phase C: task JSON 파일 생성 없음 — DB에만 기록
PLAN_NAME="{PlanName}"
BRANCH="{브랜치명}"
NOW=$(date -u +%Y-%m-%dT%H:%M:%SZ)

# DB upsert-job (필수 — 실패 시 오류 출력 후 중단)
python .github/scripts/db_write.py \
  --action upsert-job \
  --branch "${BRANCH}" \
  --sprint "${SPRINT_NUM}" \
  --task "${PLAN_NAME}" \
  --status "analyzing" \
  --created-at "${NOW}"
```

---

### 4단계: SPRINT.md 업데이트 및 커밋

**Case A — 새 브랜치 (오픈 PR 없음)**:

1. `AI/sprints/sprint-{NNN}.md` 파일을 새로 생성한다 (NNN = 3자리 0-padding SPRINT_NUM).
   파일 내용: 목표, 태스크 항목, 배경, 참조 섹션.

2. `AI/SPRINT.md` Active Sprint 테이블에 새 행을 추가하고, 이전 Active Sprint를 Recent Sprints로 이동한다.

**Case B — 기존 브랜치 (오픈 PR 존재)**: 가장 최근 스프린트의 `### 진행 중` 섹션에 태스크를 추가한다.
- 3.2·3.5단계를 건너뛰었으므로 DB에서 sprint 번호를 읽는다.

두 경우 모두:
- 사용자 설명을 기반으로 구체적인 태스크 항목 2~5개를 `- [ ]` 형식으로 작성한다.
- 항목은 검증 가능한 단위로 쪼갠다.

```bash
NNN=$(printf "%03d" "${SPRINT_NUM}")
git add AI/SPRINT.md AI/sprints/sprint-${NNN}.md
git commit -m "계획: ${PLAN_NAME} 스프린트 등록"
git push
```

---

### 5단계: 완료 보고

```
✅ /plan 완료 — Stage 1: 브랜치·DB 초기화

브랜치: {브랜치명}
스프린트: #{SPRINT_NUM}  ← SPRINT.md의 새 헤더 번호와 동일
DB job: sdlc.ai_jobs.branch = {브랜치명} (task JSON 없음 — Phase C)

작업 요약:
  목적: {작업 목적}
  범위: {예상 변경 범위}
  방향: {구현 방향}

SPRINT.md 추가 항목:
  - [ ] ...

다음 단계:
  /requirement  — 요구사항 상세 분석 + 명세 파일 생성 (Stage 2, 권장)
  /impact       — 영향 범위 분석 (코드 수정 전 실행 권장)
  /start        — 코딩 시작 선언
```
