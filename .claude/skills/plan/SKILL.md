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
- 현재 브랜치 PR 상태: !`python3 -c "import subprocess,os; env=dict(os.environ,PATH=os.environ.get('PATH','')+os.pathsep+'/c/Program Files/GitHub CLI'); b=subprocess.check_output(['git','branch','--show-current']).decode().strip(); r=subprocess.run(['gh','pr','list','--head',b,'--state','open','--json','number,state,title','--limit','1'],capture_output=True,text=True,env=env) if b and b!='main' else None; print((r.stdout.strip() or '[]') if r and r.returncode==0 else '[]')"`

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

### 3.5단계: task JSON 파일 초기화 및 커밋

브랜치 생성 직후 task JSON을 생성하고 **즉시 브랜치에 커밋**한다.
스프린트 번호는 `AI/SPRINT.md`에서 `^## 스프린트 #` 줄 수를 세어 결정한다.

```bash
SPRINT_NUM=$(grep -c "^## 스프린트 #" AI/SPRINT.md 2>/dev/null || echo "0")
PLAN_NAME="{PlanName}"
BRANCH="{브랜치명}"
NOW=$(date -u +%Y-%m-%dT%H:%M:%SZ)
mkdir -p AI/tasks
cat > "AI/tasks/sprint${SPRINT_NUM}_${PLAN_NAME}.json" << EOF
{
  "sprint": ${SPRINT_NUM},
  "task": "${PLAN_NAME}",
  "branch": "${BRANCH}",
  "status": "analyzing",
  "created_at": "${NOW}",
  "completed_at": null,
  "pr_url": null,
  "retry_count": 0,
  "last_error": null,
  "artifacts": [],
  "test_generated": false,
  "review_completed": false,
  "duration_sec": null,
  "consume_tokens": null,
  "cache_tokens": null,
  "impact": null,
  "steps": [
    {
      "name": "plan",
      "status": "done",
      "started_at": "${NOW}",
      "completed_at": "${NOW}",
      "summary": "{1단계에서 작성한 작업 목적 1문장}"
    }
  ]
}
EOF

git add "AI/tasks/sprint${SPRINT_NUM}_${PLAN_NAME}.json"
git commit -m "계획[1/2]: ${PLAN_NAME} task JSON 초기화"
git push
```

---

### 4단계: SPRINT.md 업데이트 및 커밋

`AI/SPRINT.md`를 읽어 가장 최근 스프린트의 `### 진행 중` 섹션에 태스크를 추가한다.
- 사용자 설명을 기반으로 구체적인 태스크 항목 2~5개를 `- [ ]` 형식으로 작성한다.
- 항목은 검증 가능한 단위로 쪼갠다.

```bash
git add AI/SPRINT.md
git commit -m "계획[2/2]: ${PLAN_NAME} SPRINT 등록"
git push
```

---

### 5단계: 완료 보고

```
✅ /plan 완료 — Stage 1: 브랜치·task JSON 초기화

브랜치: {브랜치명}
스프린트: #{SPRINT_NUM}
task JSON: AI/tasks/sprint{N}_{PlanName}.json

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
