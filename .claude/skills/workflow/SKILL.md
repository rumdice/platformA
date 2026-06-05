---
name: workflow
schema_version: 1
description: 계획 파일을 읽어 plan → requirement → impact → start → 코딩 → test-gen → done → review → pr 전체 파이프라인을 자동 실행한다. 사람 개입 없이 PR 생성까지 완주한다.
disable-model-invocation: false
allowed-tools: Bash(git *) Bash(gh *) Bash(dotnet *) Bash(grep *) Bash(ls *) Bash(python3 *) Bash(date *) Bash(mkdir *) Read Edit Write Agent
---

# 완전 자동화 워크플로 오케스트레이터

## 목적

사람은 계획 파일만 제공하고 PR 검수·머지만 담당한다.
이 스킬은 plan부터 pr까지 전체 파이프라인을 자동으로 실행한다.

## 컨텍스트

- 현재 브랜치: !`git branch --show-current`
- 계획 파일 인수: $ARGUMENTS
- 오늘 날짜: !`date +%Y-%m-%d`
- .claude/plan/ 미처리 파일: !`ls .claude/plan/*.md 2>/dev/null | grep -v processed | head -3 || echo "(없음)"`

---

## 수행 순서

### 0단계: 계획 소스 결정

아래 우선순위로 이번 실행의 계획 소스를 결정한다.

**1순위**: `$ARGUMENTS`에 텍스트가 있으면 → 해당 텍스트를 작업 설명으로 사용한다.

**2순위**: `.claude/plan/` 폴더에 `processed/`로 이동되지 않은 `.md` 파일이 있으면 → 가장 최근 파일을 소스로 사용한다.

```bash
ls .claude/plan/*.md 2>/dev/null | grep -v "processed" | sort | tail -1
```

**소스 없음**: 사용자에게 작업 설명을 요청하고 중단한다.

계획 소스를 확인했으면 한 줄로 출력한다:
```
소스: {파일명 또는 인수 텍스트 앞 50자}
```

---

### 1단계: /plan 실행

Skill 도구로 `/plan`을 호출한다. 인수는 계획 소스의 작업 설명 텍스트.

```
/plan {작업 설명}
```

성공하면 생성된 브랜치명과 스프린트 번호를 기억한다.
실패하면 오류를 출력하고 **중단**한다.

---

### 2단계: /requirement 실행

Skill 도구로 `/requirement`를 호출한다.

성공 판정:
- `adr_required: false` → 계속 진행
- `adr_required: true` → 아래 메시지 출력 후 **중단**:
  ```
  ⛔ 신규 ADR이 필요합니다. /adr {주제}를 실행하고 /workflow를 재시작하세요.
  ```

---

### 3단계: /impact 실행

Skill 도구로 `/impact`를 호출한다.

---

### 4단계: /start 실행

Skill 도구로 `/start`를 호출한다.
작업 지시서 내용을 화면에 출력한다.

---

### 5단계: 코딩 (LLM 직접 구현)

명세 파일의 **상세 요구사항**과 **구현 접근 방향** 섹션을 기반으로 코드를 직접 수정한다.

구현 진행 방식:
- 파일별로 순서대로 수정한다.
- 각 파일 수정 완료 후 진행 상황을 한 줄로 출력한다.
- 모든 수정이 완료되면 요약을 출력한다.

---

### 6단계: /test-gen 실행

Skill 도구로 `/test-gen`을 호출한다.

---

### 7단계: /done 실행 (최대 3회 재시도)

Skill 도구로 `/done`을 호출한다.

실패 시 재시도 로직:
```
1회 실패 → 오류 원인 분석 → 수정 시도 → /done 재실행
2회 실패 → 동일 과정 반복
3회 실패 → 중단 후 아래 메시지 출력:
  ⛔ /done 3회 실패. 수동 수정이 필요합니다.
  오류 내용: {오류 메시지}
  수정 후 /done을 직접 실행하세요.
```

---

### 8단계: /review 실행

Skill 도구로 `/review`를 호출한다.

리뷰 결과에서 **위반 항목**이 있으면:
- 심각도 HIGH: 자동 수정 시도 → /done 재실행 → /review 재실행
- 심각도 LOW/MINOR: 위반 사항을 출력하고 계속 진행

---

### 9단계: /pr 실행

Skill 도구로 `/pr`을 호출한다.

---

### 완료 보고

```
파이프라인 완료 — {PlanName}

PR: {PR URL}

사람이 할 일:
  1. PR 내용 검토
  2. GitHub에서 승인 후 머지
  3. 머지 후 자동 동기화 완료

자동 처리된 단계:
  plan → requirement → impact → start → 코딩 → test-gen → done → review → pr
```

---

## 주의사항

- 각 단계 실패 시 즉시 중단하고 원인을 명확히 보고한다.
- /done 재시도 중 발생하는 포맷 오류는 `dotnet format`으로 먼저 수정한다.
- 명세 파일이 `.claude/plan/`에 있어야 /requirement가 올바르게 동작한다.
- adr_required=true인 경우 /workflow를 재시작하기 전에 반드시 /adr을 먼저 실행해야 한다.
