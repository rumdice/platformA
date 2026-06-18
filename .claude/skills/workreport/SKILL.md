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

### 1.5단계: 프로젝트 완성도 데이터 수집

각 프로젝트의 완성도를 평가하기 위한 데이터를 수집한다.

**Dockerfile 존재 여부:**
```bash
for proj in PlatformA.Auth.API PlatformA.Ticketing.API PlatformA.Matching.API PlatformA.Game.Server PlatformA.Utils.API; do
  echo -n "$proj Dockerfile: "
  [ -f "PlatformA/$proj/Dockerfile" ] && echo "있음" || echo "없음"
done
```

**헬스체크 구현 여부 (API 프로젝트):**
```bash
for proj in PlatformA.Auth.API PlatformA.Ticketing.API PlatformA.Matching.API PlatformA.Utils.API; do
  echo -n "$proj HealthCheck: "
  FOUND=$(grep -rl "MapHealthChecks\|IHealthCheck" PlatformA/$proj/ 2>/dev/null | grep -v "bin\|obj" | head -1)
  [ -n "$FOUND" ] && echo "있음" || echo "없음"
done
```

**테스트 프로젝트 및 테스트 수:**
```bash
for proj in Auth.API Utils.API Ticketing.API Matching.API Game.Server Game.Gomoku; do
  TEST_DIR="PlatformA/PlatformA.Tests.${proj}"
  if [ -d "$TEST_DIR" ]; then
    COUNT=$(grep -rl "\[Fact\]\|\[Theory\]" "$TEST_DIR" 2>/dev/null | xargs grep -h "\[Fact\]\|\[Theory\]" 2>/dev/null | wc -l)
    echo "$proj: ${COUNT}개 테스트"
  else
    echo "$proj: 테스트 없음"
  fi
done
```

**API 문서 존재 여부:**
```bash
ls Docs/api-guide/ 2>/dev/null
```

**Library.Game 구현 현황:**
```bash
find PlatformA/PlatformA.Library.Game -name "*.cs" 2>/dev/null | grep -v "bin\|obj" | wc -l
ls PlatformA/PlatformA.Library.Game/ 2>/dev/null
```

**최근 sprint 이력 (프로젝트별 완료된 기능 파악):**
```bash
grep -h "Auth\|Ticketing\|Matching\|Game\|Utils\|Library" AI/sprints/sprint-0*.md 2>/dev/null | grep "^\- \[x\]" | tail -30
```

수집된 데이터를 바탕으로 각 프로젝트의 완성도(%)를 아래 기준으로 LLM이 판단한다:
- **빌드/테스트** (30점): 테스트 존재 및 최근 빌드 통과 여부
- **핵심 기능** (30점): sprint 이력 기반 주요 기능 구현 완료 비율
- **운영 준비** (25점): Dockerfile + 헬스체크 구현 여부
- **문서화** (15점): API 문서 존재 여부 (API 서비스만 해당, 나머지는 full)

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

## 프로젝트 완성도 현황

> 1.5단계에서 수집한 데이터를 기반으로 LLM이 평가한다.

| 프로젝트 | 완성도 | 빌드/테스트 | 핵심 기능 | 운영 준비 | 문서화 | 비고 |
|---------|--------|------------|---------|---------|------|------|
| Auth.API | NN% | ✅/❌ | ✅/❌ | ✅/❌ | ✅/❌ | {주요 미완성 기능 또는 "–"} |
| Ticketing.API | NN% | ✅/❌ | ✅/❌ | ✅/❌ | ✅/❌ | {비고} |
| Matching.API | NN% | ✅/❌ | ✅/❌ | ✅/❌ | ✅/❌ | {비고} |
| Game.Server | NN% | ✅/❌ | ✅/❌ | ✅/❌ | N/A | {비고} |
| Utils.API | NN% | ✅/❌ | ✅/❌ | ✅/❌ | ✅/❌ | {비고} |
| Library.Game | NN% | ✅/❌ | ✅/❌ | N/A | N/A | {비고} |

**전체 플랫폼 완성도**: NN% — {한 줄 코멘트}

---

## 오늘 작업 피드백 및 개선점

> 오늘 머지된 PR과 완료된 스프린트를 분석하여 LLM이 1~5가지를 작성한다.
> 잘된 점과 개선하면 좋을 점을 균형 있게 포함한다.

### 1. {피드백 제목}

**잘된 점**: {구체적인 잘된 부분}
**개선하면 좋을 점**: {다음에 더 잘할 수 있는 방향}

### 2. {피드백 제목}

**잘된 점**: ...
**개선하면 좋을 점**: ...

{추가 항목 — 오늘 작업량에 따라 1~5개}

---

## 종료 시점 상태

- 오픈 PR: N건 / 없음
- 스프린트 #N 상태: 진행 중 / 완료
- 특이사항: 없음
```

**작성 원칙:**
- 각 PR 항목은 **무엇을 했고 왜 했는지** 모두 담는다
- DB(ai_model_runs) 또는 sprint 파일에서 규모(S/M/L/XL)를 파악하여 작업 설명에 반영한다
- 오늘 머지된 PR이 없으면 "오늘 머지된 PR이 없습니다." 한 줄로 섹션을 대체한다
- 기술 용어는 그대로 사용한다
- **프로젝트 완성도**: 1.5단계 데이터 기반, LLM이 직접 평가 — 단순 체크박스 복사 금지
- **피드백**: 오늘 실제 작업 흐름 분석 기반 — 일반론이 아닌 오늘에 특화된 내용으로 작성

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
