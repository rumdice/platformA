---
name: requirement
schema_version: 1
description: /plan 완료 후 브랜치에서 요구사항을 상세 분석하고 구현 명세(.md)를 생성한다. 명세 파일을 브랜치에 커밋하고 task JSON에 requirement 단계를 기록한다. 워크플로우 Stage 2.
allowed-tools: Bash(git *) Bash(ls *) Bash(grep *) Bash(python3 *) Read Edit Write
---

# /requirement — 요구사항 상세 분석 및 명세 생성 (Stage 2)

> **전제**: `/plan`으로 브랜치와 task JSON이 이미 생성된 상태에서 실행한다.

## 인수
```
/requirement                   → /plan의 작업 설명 + plan mode 파일 자동 탐지
/requirement [추가 요구사항]    → 추가 텍스트를 보완 요구사항으로 사용
```

## 컨텍스트
- 현재 브랜치: !`git branch --show-current`
- 오늘 날짜: !`date +%Y-%m-%d`
- 오늘 날짜 기준 명세 파일: !`python3 -c "import glob,datetime; t=datetime.date.today().strftime('%Y-%m-%d'); f=sorted(glob.glob('.claude/plan/'+t+'_*.md')); print('\n'.join(f) if f else '(없음)')"`
- 현재 브랜치 task JSON: !`python3 -c "import glob,json,subprocess; b=subprocess.check_output(['git','branch','--show-current']).decode().strip(); files=glob.glob('AI/tasks/sprint*.json'); match=[f for f in files if json.load(open(f)).get('branch')==b]; print(match[0] if match else '(없음)')"`
- 외부 제출 파일: !`ls .claude/plan/*.md 2>/dev/null | grep -v README | head -3 || echo "(없음)"`
- plan mode 파일: !`ls ~/.claude/plans/*.md 2>/dev/null | grep -vE '/[0-9]{4}-[0-9]{2}-[0-9]{2}_' | head -1 || echo "(없음)"`

---

## 수행 순서

### 사전 검사

현재 브랜치가 `main`이면 `/plan`을 먼저 실행하라고 안내하고 **중단**한다.

---

### 0단계 — task JSON 및 작업 컨텍스트 파악

현재 브랜치에 해당하는 task JSON을 읽어 PlanName, 브랜치명, 작업 요약(plan 단계 summary)을 파악한다.
task JSON이 없으면 `/plan`을 먼저 실행하라고 안내하고 **중단**한다.

---

### 사전 검사 — 요구사항 소스 결정

아래 우선순위로 요구사항 소스를 결정한다:

**1순위: `.claude/plan/*.md` (외부 제출 파일)**
파일이 있으면 → 해당 파일 내용을 읽어 소스로 사용한다.

**2순위: plan mode 파일 (동적 탐지)**
`~/.claude/plans/*.md` 중 날짜 접두사 없는 파일(ex. `aws-wiggly-bee.md`)을 탐지한다.
파일이 있고 내용이 있으면 → 해당 파일 내용을 소스로 사용한다.

**3순위: `$ARGUMENTS` 텍스트 + task JSON의 plan summary**
추가 요구사항이 없으면 task JSON의 plan summary(작업 목적)를 기반으로 분석한다.

**소스 없음:**
> task JSON의 plan summary를 요구사항 소스로 사용하여 진행한다.

---

### 1단계 — 요구사항 상세 분석

소스 원본과 출처를 명시한 뒤 아래 항목으로 분석한다:

- **목적**: 무엇을 달성하려는가?
- **범위**: 어떤 서비스/모듈이 영향을 받는가?
- **제약**: 기존 ADR, 보안 정책, 성능 요구사항
- **오류/누락 체크**: 불명확하거나 모순된 부분이 있으면 **질문**한다

불명확한 사항이 있으면 구현을 진행하기 전에 반드시 사용자에게 확인한다.

---

### 2단계 — 태스크 번호 결정

오늘 날짜로 이미 생성된 `.claude/plan/` 파일 수를 세어 다음 번호를 결정한다:

```python
import glob, datetime
t = datetime.date.today().strftime('%Y-%m-%d')
existing = len(glob.glob(f'.claude/plan/{t}_*.md'))
task_num = f"{existing + 1:03d}"
```

---

### 3단계 — PlanName 결정

task JSON의 `task` 필드에서 PlanName을 읽는다.
(task JSON이 없는 경우: 요구사항에서 PascalCase 영문 PlanName을 생성한다.)

---

### 4단계 — 구현 명세 파일 생성

`.claude/plan/YYYY-MM-DD_NNN_PlanName.md` 형식으로 프로젝트 경로에 파일을 생성한다.

파일 내용 구조:
```markdown
# 요구사항 명세: {PlanName}

작성일: {YYYY-MM-DD}
브랜치: {브랜치명}
소스: {plan mode | .claude/plan/{파일명} | 직접 입력 | task JSON summary}

## 요구사항 요약
{1-3줄 요약}

## 상세 요구사항
{번호별 상세 요구사항}

## 영향 범위 (예상)
{어떤 서비스/파일이 변경될 것인지 예상}

## 제약 및 주의사항
{ADR 참조, 보안 정책, 성능 제약 등}

## 구현 접근 방향
{권장 구현 방식}

## 검증 기준
{완료 조건 — 어떻게 하면 이 요구사항이 충족되었다고 볼 수 있는가}
```

---

### 5단계 — DESIGN_REVIEW (ADR 대조)

기존 ADR 목록을 확인하여 요구사항이 기결정 사항과 충돌하는지 검토한다:

```bash
ls AI/adr/ 2>/dev/null | sort
```

검토 항목:
- 요구사항의 **접근 방식**이 기존 ADR과 충돌하는가?
- 새로운 **설계 결정**이 필요한가? (기존 ADR에 없는 새 기술/패턴/버전 정책 도입 시)

출력 형식:
```
## DESIGN_REVIEW 결과

| ADR | 관련 여부 | 충돌/참고 사항 |
|-----|---------|--------------|
| ADR-NNN: 제목 | 관련 있음 / 없음 | {내용} |

판정: ✅ 기존 ADR 준수 | ⚠️ ADR 충돌 있음 | 📝 신규 ADR 필요
```

**판정이 `📝 신규 ADR 필요`인 경우 — 자동 연계 (필수)**

task JSON의 `adr_required` 필드를 `true`로 갱신한다 (Edit 도구):
```json
"adr_required": true
```

그 후 아래 메시지를 출력하고 **중단**한다. ADR 생성 없이 다음 단계로 진행하면
`/pr`과 GitHub Actions gate-check가 PR을 차단한다:

```
⛔ DESIGN_REVIEW: 신규 ADR이 필요합니다.

결정 주제: {ADR 주제}
이유: {기존 ADR에 없는 새 결정 사항 요약}

▶ 지금 실행하세요:
  /adr {ADR 주제}

ADR 생성 완료 후 task JSON에서 adr_required를 false로 변경하고
/requirement를 다시 실행하거나 /start로 진행하세요.
```

ADR 충돌 시: 사용자에게 보고하고 요구사항 조정 여부를 확인한다.

---

### 6단계 — 명세 파일 브랜치에 커밋

명세 파일과 소스 파일 처리를 묶어 한 번의 커밋으로 브랜치에 기록한다.

소스가 `.claude/plan/*.md` 외부 파일이었다면 먼저 이동한다:
```bash
mv ".claude/plan/{원본파일명}" ".claude/plan/processed/{YYYY-MM-DD}_{원본파일명}"
```

그 후 커밋:
```bash
git add .claude/plan/
git commit -m "요구사항: {PlanName} 명세 파일 생성"
git push
```

---

### 7단계 — task JSON에 requirement 단계 기록

Edit 도구로 task JSON의 `steps[]` 배열에 requirement 단계를 추가한다:

```json
{
  "name": "requirement",
  "status": "done",
  "started_at": "{ISO8601}",
  "completed_at": "{ISO8601}",
  "summary": "{요구사항 요약 1문장 + DESIGN_REVIEW 판정}"
}
```

---

### 8단계 — 완료 보고

```
✅ /requirement 완료 — Stage 2: 요구사항 명세 생성

소스: {출처}
명세: .claude/plan/{YYYY-MM-DD}_{NNN}_{PlanName}.md (브랜치에 커밋됨)

요약:
- 목적: {요약}
- 영향 범위: {서비스 목록}
- 종합 위험도: 🔴 HIGH | 🟡 MEDIUM | 🟢 LOW
- DESIGN_REVIEW: ✅ ADR 준수 | ⚠️ 충돌 | 📝 신규 ADR 필요

다음 단계:
  /impact  — 영향 범위 분석 (코드 수정 전 실행 권장)
  /start   — 코딩 시작 선언 (task 상태 coding 전환)
```

---

## 사용 예시

```
/requirement                          → 자동 탐지 (plan 폴더 또는 plan mode 파일)
/requirement 추가로 rate limit도 적용  → 추가 요구사항 보완
```
