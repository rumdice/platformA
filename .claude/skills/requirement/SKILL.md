---
name: requirement
schema_version: 1
description: 사용자가 제출한 요구사항을 분석하고 검토하여 구현 명세(.md)를 생성한다. REQUIREMENT_ANALYSIS 단계를 수행하며, 완료 시 프로젝트 .claude/plan/ 경로에 명세 파일을 저장한다.
allowed-tools: Bash(date *) Bash(ls *) Bash(mv *) Read Edit Write
---

# /requirement — 요구사항 분석 및 명세 생성

## 인수
```
/requirement                     → 자동 소스 탐지 (우선순위 순)
/requirement [요구사항 텍스트]     → 텍스트를 요구사항으로 사용
/requirement [파일경로.md]         → 지정 파일 내용을 요구사항으로 사용
```

## 컨텍스트
- 오늘 날짜: !`date +%Y-%m-%d`
- 오늘 날짜 기준 명세 파일: !`ls .claude/plan/$(date +%Y-%m-%d)_*.md 2>/dev/null || echo "(없음)"`
- 프로젝트 plan 제출 파일: !`ls .claude/plan/*.md 2>/dev/null | grep -v README | head -5 || echo "(없음)"`
- plan mode 파일: !`ls ~/.claude/plans/*.md 2>/dev/null | grep -vE '/[0-9]{4}-[0-9]{2}-[0-9]{2}_' | head -1 || echo "(없음)"`

---

## 수행 순서

### 사전 검사 — 소스 결정

아래 우선순위로 요구사항 소스를 결정한다:

**1순위: `.claude/plan/*.md` (프로젝트 제출 파일)**
```bash
SOURCE_FILE=$(ls .claude/plan/*.md 2>/dev/null | grep -v README | sort | head -1)
```
파일이 있으면 → 해당 파일 내용을 읽어 소스로 사용한다.

**2순위: plan mode 파일 (동적 탐지)**
```bash
PLAN_MODE_FILE=$(ls ~/.claude/plans/*.md 2>/dev/null \
  | grep -vE '/[0-9]{4}-[0-9]{2}-[0-9]{2}_' \
  | head -1)
```
`YYYY-MM-DD_` 로 시작하는 파일(구 포맷·신 포맷 모두)을 제외하고,
`aws-wiggly-bee.md` 같은 plan mode 전용 파일만 탐지한다.
파일이 있고 내용이 있으면 → 해당 파일 내용을 소스로 사용한다.

**3순위: `$ARGUMENTS` 파일 경로**
인수가 `.md`로 끝나면 → 해당 파일을 읽어 소스로 사용한다.

**4순위: `$ARGUMENTS` 텍스트**
그 외 텍스트 인수 → 그대로 요구사항으로 사용한다.

**소스 없음:**
> "요구사항 소스를 찾을 수 없습니다.
>  - .claude/plan/ 에 계획 파일을 저장하거나
>  - plan mode를 실행한 뒤 /requirement 를 실행하거나
>  - '/requirement 요구사항 텍스트' 형식으로 직접 입력하세요."
> 중단한다.

---

### 1단계 — 요구사항 분석

소스 원본과 출처를 명시한 뒤 아래 항목으로 분석한다:

- **목적**: 무엇을 달성하려는가?
- **범위**: 어떤 서비스/모듈이 영향을 받는가?
- **제약**: 기존 ADR, 보안 정책, 성능 요구사항
- **오류/누락 체크**: 불명확하거나 모순된 부분이 있으면 **질문**한다

불명확한 사항이 있으면 구현을 진행하기 전에 반드시 사용자에게 확인한다.

---

### 2단계 — 태스크 번호 결정

오늘 날짜로 이미 생성된 `.claude/plan/` 파일 수를 세어 다음 번호를 결정한다:

```bash
EXISTING=$(ls .claude/plan/$(date +%Y-%m-%d)_*.md 2>/dev/null | wc -l)
TASK_NUM=$(printf "%03d" $((EXISTING + 1)))
```

---

### 3단계 — PlanName 생성

요구사항에서 PascalCase 영문 PlanName을 생성한다.
- 규칙: 동사+명사, 최대 30자 (예: FixRedisBug, AddMatchingTests)

---

### 4단계 — 구현 명세 파일 생성

`.claude/plan/YYYY-MM-DD_NNN_PlanName.md` 형식으로 프로젝트 경로에 파일을 생성한다.

파일 내용 구조:
```markdown
# 요구사항 명세: {PlanName}

작성일: {YYYY-MM-DD}
소스: {plan mode | .claude/plan/{파일명} | 직접 입력}

## 요구사항 요약
{1-3줄 요약}

## 상세 요구사항
{번호별 상세 요구사항}

## 영향 범위 (예상)
{어떤 서비스/파일이 변경될 것인지 예상}

## 제약 및 주의사항
{ADR 참조, 보안 정책, 성능 제약 등}

## 구현 접근 방향
{권장 구현 방식 — 상세 설계는 /plan 에서}

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
- 요구사항의 **접근 방식**이 기존 ADR(특히 Redis 키 규칙, DB 마이그레이션, 패킷 직렬화 등)과 충돌하는가?
- 새로운 **설계 결정**이 필요한가? (기존 ADR에 없는 새 전략/패턴 도입 시)

출력 형식:
```
## DESIGN_REVIEW 결과

| ADR | 관련 여부 | 충돌/참고 사항 |
|-----|---------|--------------|
| ADR-NNN: 제목 | 관련 있음 / 없음 | {내용} |

판정: ✅ 기존 ADR 준수 | ⚠️ ADR 충돌 있음 | 📝 신규 ADR 권장
```

신규 ADR이 필요한 경우: `/adr {결정 주제}` 실행 후 명세 파일을 보완한다.
ADR 충돌 시: 사용자에게 보고하고 요구사항 조정 여부를 확인한다.

---

### 6단계 — 소스 파일 처리

소스가 `.claude/plan/*.md` 파일이었다면 처리 완료 후 이동한다:
```bash
mv ".claude/plan/{원본파일명}" ".claude/plan/processed/{YYYY-MM-DD}_{원본파일명}"
```

---

### 7단계 — 완료 보고

```
✅ 요구사항 명세 생성 완료

소스: {출처}
파일: .claude/plan/{YYYY-MM-DD}_{NNN}_{PlanName}.md

요약:
- 목적: {요약}
- 영향 범위: {서비스 목록}
- 종합 위험도: 🔴 HIGH | 🟡 MEDIUM | 🟢 LOW
- DESIGN_REVIEW: ✅ ADR 준수 | ⚠️ 충돌 | 📝 신규 ADR 필요

다음 단계:
  /plan {PlanName}  — 브랜치 생성 및 구현 계획 수립
```

---

## 사용 예시

```
/requirement                                  → 자동 탐지 (plan 폴더 또는 plan mode 파일)
/requirement Redis 분산 락 해제 누락 수정      → 텍스트 직접 입력
/requirement .claude/plan/temp_plan.md        → 특정 파일 지정
```
