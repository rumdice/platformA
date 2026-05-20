---
name: requirement
schema_version: 1
description: 사용자가 제출한 요구사항을 분석하고 검토하여 구현 명세(.md)를 생성한다. REQUIREMENT_ANALYSIS 단계를 수행하며, 완료 시 plans/ 경로에 명세 파일을 저장한다.
allowed-tools: Bash(date *) Read Edit Write
---

# /requirement — 요구사항 분석 및 명세 생성

## 인수
```
/requirement [요구사항 설명 또는 없음]
```
- 인수가 있으면 해당 내용을 요구사항으로 사용한다
- 인수가 없으면 사용자에게 요구사항을 입력받는다

## 컨텍스트
- 오늘 날짜: !`date +%Y-%m-%d`
- 기존 plans 파일 수: !`ls C:/Users/rumdi/.claude/plans/*.md 2>/dev/null | wc -l || echo 0`
- 오늘 날짜 기준 plans 파일: !`ls C:/Users/rumdi/.claude/plans/$(date +%Y-%m-%d)_*.md 2>/dev/null || echo "(없음)"`

---

## 수행 순서

### 사전 검사

`$ARGUMENTS`가 비어 있으면:
> "요구사항을 입력하세요. 예: '/requirement Redis 분산 락 해제 누락 버그 수정'"
> 중단한다.

---

### 1단계 — 요구사항 분석

입력된 요구사항을 아래 항목으로 분석한다:

- **목적**: 무엇을 달성하려는가?
- **범위**: 어떤 서비스/모듈이 영향을 받는가?
- **제약**: 기존 ADR, 보안 정책, 성능 요구사항
- **오류/누락 체크**: 불명확하거나 모순된 부분이 있으면 **질문**한다

불명확한 사항이 있으면 구현을 진행하기 전에 반드시 사용자에게 확인한다.

---

### 2단계 — 태스크 번호 결정

오늘 날짜로 이미 생성된 plans 파일 수를 세어 다음 번호를 결정한다:

```bash
EXISTING=$(ls C:/Users/rumdi/.claude/plans/$(date +%Y-%m-%d)_*.md 2>/dev/null | wc -l)
TASK_NUM=$(printf "%03d" $((EXISTING + 1)))
```

---

### 3단계 — PlanName 생성

요구사항에서 PascalCase 영문 PlanName을 생성한다.
- 규칙: 동사+명사, 최대 30자 (예: FixRedisBug, AddMatchingTests)

---

### 4단계 — 구현 명세 파일 생성

`C:\Users\rumdi\.claude\plans\YYYY-MM-DD_NNN_PlanName.md` 형식으로 파일을 생성한다.

파일 내용 구조:
```markdown
# 요구사항 명세: {PlanName}

작성일: {YYYY-MM-DD}
요청자: 사용자

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

### 5단계 — 완료 보고

```
✅ 요구사항 명세 생성 완료

파일: C:\Users\rumdi\.claude\plans\{YYYY-MM-DD}_{NNN}_{PlanName}.md

요약:
- 목적: {요약}
- 영향 범위: {서비스 목록}
- 종합 위험도: 🔴 HIGH | 🟡 MEDIUM | 🟢 LOW

다음 단계:
  /plan {PlanName}  — 브랜치 생성 및 구현 계획 수립
  /impact           — 변경 영향 범위 상세 분석 (선택)
```

---

## 사용 예시

```
/requirement Redis 분산 락 해제 누락 수정
/requirement 매칭 API 통합 테스트 추가
/requirement 인증 토큰 만료 시간 환경 변수로 분리
```
