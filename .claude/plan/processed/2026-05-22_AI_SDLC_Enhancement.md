# PLAN — AI_SDLC Gate 강화 및 상태 정합성 개선

작성일: 2026-05-22  
대상 프로젝트: PlatformA  
작업 목적: 5월 22일 기준 AI_SDLC 파이프라인 개선사항을 반영하여, Claude Code가 다음 작업에서 수행할 수 있는 구체적인 실행 계획을 정의한다.

---

## 0. 배경

현재 PlatformA는 AI 기반 SDLC 파이프라인을 다음 흐름으로 정리하고 있다.

```text
/requirement
→ /plan
→ /impact
→ /start
→ 코딩
→ /test-gen
→ /done
→ /pr
```

최근 개선으로 다음은 완료되었다.

- `/done` 역할을 BUILD_GATE로 축소
- `/pr`가 PR_SUMMARY와 완료 처리를 담당
- `/test-gen` 스킬 추가
- `test_generated`, `review_completed` 필드 추가
- `/review` 실행 결과를 task JSON에 기록
- `AI/tasks/SCHEMA.md` 상태 머신 갱신

그러나 아직 다음 문제가 남아 있다.

- `/test-gen`, `/review`가 필수 게이트로 강제되지 않음
- `/impact` 결과가 task JSON에 구조적으로 저장되지 않음
- `SCHEMA.md` 예시 JSON에 구형 상태값이 남아 있음
- task JSON이 단계별 실행 이력을 충분히 기록하지 못함
- `/pr` 실행 시 위험도, 테스트 생성 여부, 리뷰 완료 여부를 검사하지 않음
- 향후 RDB 전환을 위한 데이터 구조가 아직 약함

이번 작업은 새로운 대규모 기능 추가가 아니라, **AI_SDLC 공정의 신뢰성 강화**를 목표로 한다.

---

## 1. 핵심 목표

이번 작업의 핵심 목표는 다음과 같다.

```text
AI_SDLC 단계가 존재하는 것에서 끝나지 않고,
각 단계를 실제로 거치도록 게이트를 강화한다.
```

즉, 다음을 달성해야 한다.

- 코드 변경이 있는데 `/test-gen` 없이 넘어가지 않도록 경고한다.
- 위험도가 높은 작업은 `/review` 없이 PR 생성하지 못하도록 한다.
- `/impact` 결과를 task JSON에 저장한다.
- task JSON이 향후 MariaDB/PostgreSQL `ai_jobs`, `ai_job_steps`로 마이그레이션 가능하도록 구조를 보강한다.
- 문서와 스킬 설명의 불일치를 제거한다.

---

## 2. 작업 범위

### 포함

- `AI/tasks/SCHEMA.md` 수정
- `.claude/skills/impact/SKILL.md` 수정
- `.claude/skills/pr/SKILL.md` 수정
- `.claude/skills/done/SKILL.md` 수정 여부 검토
- `.claude/skills/test-gen/SKILL.md` 수정 여부 검토
- `.claude/skills/review/SKILL.md` 수정 여부 검토
- `AI/AI_SDLC(pipeline).txt` 갱신
- `AI/SPRINT.md` 태스크 추가/완료 체크

### 제외

- MariaDB/PostgreSQL 실제 도입
- n8n 도입
- LLM Router 도입
- GitHub Branch Protection 실제 설정 자동화
- 완전 자동 배포
- 게임 서버 기능 추가

---

## 3. 상세 작업

---

## TASK 1. `AI/tasks/SCHEMA.md` 예시 JSON 정합성 수정

### 문제

현재 `AI/tasks/SCHEMA.md`의 예시 JSON에 구형 상태값이 남아 있을 수 있다.

예:

```json
"status": "in_progress"
```

하지만 현재 상태 머신은 다음을 사용한다.

```text
pending → analyzing → coding → testing → done
                                   ↓
                                failed
```

### 작업

예시 JSON을 최신 구조로 수정한다.

권장 예시:

```json
{
  "sprint": 24,
  "task": "ImproveSdlcGates",
  "branch": "2026-05-22_ImproveSdlcGates",
  "status": "analyzing",
  "created_at": "2026-05-22T00:00:00Z",
  "completed_at": null,
  "pr_url": null,
  "retry_count": 0,
  "last_error": null,
  "artifacts": [],
  "test_generated": false,
  "review_completed": false,
  "impact": null,
  "steps": []
}
```

### 완료 기준

- `in_progress` 예시 제거
- 신규 필드 `impact`, `steps` 설명 추가
- 기존 상태 머신 설명과 예시가 일치해야 함

---

## TASK 2. task JSON에 `impact` 필드 추가

### 목적

`/impact` 결과를 대화 출력으로만 남기지 않고, 이후 `/pr` 게이트에서 사용할 수 있도록 task JSON에 저장한다.

### 권장 구조

```json
"impact": {
  "risk": "LOW | MEDIUM | HIGH",
  "changed_files": 0,
  "high_risk_files": [],
  "medium_risk_files": [],
  "low_risk_files": [],
  "test_coverage": "none | partial | full | not_required",
  "summary": "영향 분석 요약"
}
```

### 작업

- `AI/tasks/SCHEMA.md`에 `impact` 필드 정의 추가
- `/plan`이 생성하는 task JSON에는 `"impact": null` 추가
- `/impact` 실행 후 task JSON의 `impact` 필드를 갱신하도록 `.claude/skills/impact/SKILL.md` 수정

### 완료 기준

- `/impact` 실행 결과가 task JSON에 저장되는 절차가 명시되어야 함
- `/pr`에서 이 값을 읽을 수 있어야 함

---

## TASK 3. task JSON에 `steps[]` 필드 추가

### 목적

현재 task JSON은 최종 상태 중심이다.  
향후 RDB 전환 및 실패 분석을 위해 단계별 실행 이력을 기록할 수 있어야 한다.

### 권장 구조

```json
"steps": [
  {
    "name": "impact",
    "status": "done",
    "started_at": "2026-05-22T00:00:00Z",
    "completed_at": "2026-05-22T00:01:00Z",
    "summary": "MEDIUM risk, 5 changed files"
  }
]
```

### 이번 작업의 현실적 범위

이번 단계에서는 모든 스킬이 `steps[]`를 완벽히 갱신하지 않아도 된다.

우선 문서와 최소 스킬에 반영한다.

우선 적용 대상:

- `/impact`
- `/test-gen`
- `/review`
- `/done`
- `/pr`

### 완료 기준

- `AI/tasks/SCHEMA.md`에 `steps[]` 필드 정의 추가
- 최소한 `/impact` 또는 `/pr` 중 하나는 `steps[]` 기록 절차를 포함해야 함
- 향후 `ai_job_steps` 테이블로 마이그레이션할 수 있음을 문서화

---

## TASK 4. `/pr`에 게이트 검사 추가

### 문제

현재 `/pr`은 PR 생성과 완료 처리를 담당하지만, 다음 조건을 강제하지 않는다.

- 테스트 생성 여부
- 리뷰 완료 여부
- 영향도 분석 위험도
- 코드 변경 여부

### 작업

`.claude/skills/pr/SKILL.md`의 사전 검사에 아래 조건을 추가한다.

### 검사 1. task JSON 존재 여부

```bash
CURRENT_BRANCH=$(git branch --show-current)
TASK_FILE=$(grep -rl "\"branch\": \"${CURRENT_BRANCH}\"" AI/tasks/ 2>/dev/null | head -1)
```

TASK_FILE이 없으면 경고한다.

> task JSON이 없습니다. `/plan`으로 생성된 작업인지 확인하세요.

단, 문서-only 긴급 작업을 위해 중단이 아니라 경고로 둘 수 있다.

### 검사 2. 코드 변경 여부 확인

```bash
CODE_CHANGED=$(git diff --name-only origin/main...HEAD 2>/dev/null \
  | grep -E '\.(cs|proto|csproj)$' || true)
```

### 검사 3. 테스트 생성 여부 확인

코드 변경이 있는데 `test_generated`가 false이면 경고 또는 중단한다.

권장 정책:

```text
Controller / Service / DTO / Game Server / Library 변경이 있으면:
  test_generated == false → /pr 중단
문서 / 스킬 / 설정만 변경이면:
  경고 없이 통과
```

### 검사 4. 리뷰 완료 여부 확인

아래 조건 중 하나라도 해당하면 `review_completed == false`일 때 `/pr`을 중단한다.

- `impact.risk == "HIGH"`
- 변경 파일 수 10개 초과
- `PlatformA.Library/` 변경
- `Migrations/`, `DbContext`, `Entities/` 변경
- `Auth`, `Token`, `Jwt`, `Redis`, `Lock` 관련 파일 변경

### 검사 5. impact 미실행 검사

코드 변경이 있는데 `impact == null`이면 `/pr` 실행 전 경고한다.

권장 정책:

```text
LOW/MEDIUM 이하 작업: 경고 후 계속 가능
HIGH 가능성이 있는 경로 변경: 중단
```

### 완료 기준

- `/pr` 사전 검사에 test/review/impact 게이트가 추가됨
- 위험도가 높은 작업은 `/review` 없이 PR 생성되지 않음
- 코드 변경이 있는 작업은 `/test-gen` 없이 PR 생성되지 않음

---

## TASK 5. `/done`에 `/test-gen` 미실행 경고 추가 검토

### 목적

`/pr`에서 최종 게이트를 수행하더라도, `/done` 단계에서 미리 알려주는 것이 좋다.

### 작업

`.claude/skills/done/SKILL.md`에 아래 경고를 추가한다.

빌드 전 또는 테스트 전:

```text
코드 변경이 감지되었으나 test_generated=false 입니다.
권장 흐름은 `/test-gen` 실행 후 `/done` 입니다.
```

정책은 중단이 아니라 경고로 둔다.

이유:

- 테스트가 필요 없는 코드 변경도 있을 수 있음
- 강제 중단은 초기 단계에서 너무 불편할 수 있음
- 최종 강제는 `/pr`에서 담당하는 것이 더 적절함

### 완료 기준

- `/done`은 BUILD_GATE 역할을 유지한다.
- `/done`이 PR 생성, SPRINT 완료, cost-log를 다시 담당하지 않도록 한다.
- 경고만 추가하고 책임을 과도하게 늘리지 않는다.

---

## TASK 6. `/impact` 결과를 `artifacts` 또는 `steps[]`에도 기록

### 목적

영향도 분석 결과가 나중에 추적 가능해야 한다.

### 작업

`/impact` 실행 후 task JSON에 다음 중 하나를 기록한다.

간단 버전:

```json
"artifacts": [
  "impact: MEDIUM risk, 6 changed files"
]
```

권장 버전:

```json
"steps": [
  {
    "name": "impact",
    "status": "done",
    "summary": "MEDIUM risk, 6 changed files"
  }
]
```

### 완료 기준

- `/impact` 결과가 task JSON에 남아야 함
- `/pr`에서 해당 결과를 읽어 게이트 판단에 사용할 수 있어야 함

---

## TASK 7. `AI/AI_SDLC(pipeline).txt` 갱신

### 작업

현재 최신 파이프라인을 명확하게 기록한다.

```text
0. USER_PLAN
1. REQUIREMENT_ANALYSIS      → /requirement
2. PLAN_BRANCH               → /plan
3. IMPACT_ANALYSIS           → /impact
4. CODE_FIX_START            → /start
5. CODE_FIX                  → Claude Code 구현
6. TEST_CASE_GENERATION      → /test-gen
7. BUILD_TEST                → /done
8. CODE_REVIEW               → /review
9. PR_SUMMARY                → /pr
```

또한 다음 정책을 추가한다.

```text
- /test-gen은 코드 변경이 있는 작업에서 /done 전에 실행 권장
- HIGH 위험도 작업은 /review 후 /pr 실행
- /pr은 test_generated/review_completed/impact 결과를 검사한다
- /done은 BUILD_GATE만 담당한다
- /pr은 완료 처리와 PR 생성만 담당한다
```

### 완료 기준

- 문서상 파이프라인과 실제 스킬 책임이 일치해야 함
- `/done`과 `/pr` 책임 경계가 명확해야 함

---

## TASK 8. `AI/SPRINT.md`에 신규 스프린트 항목 추가 및 완료 체크

### 작업

`AI/SPRINT.md` 맨 아래에 신규 스프린트를 추가한다.

예:

```markdown
---

## 스프린트 #25 (2026-05-22 ~)
**목표**: AI_SDLC Gate 강화 — impact/test/review 결과를 task JSON 기반으로 PR 단계에서 검사

### 진행 중

- [ ] `AI/tasks/SCHEMA.md` — impact/steps 필드 추가 및 예시 JSON 최신화
- [ ] `.claude/skills/impact/SKILL.md` — impact 결과 task JSON 저장
- [ ] `.claude/skills/pr/SKILL.md` — test_generated/review_completed/impact 게이트 검사 추가
- [ ] `.claude/skills/done/SKILL.md` — test-gen 미실행 경고 추가
- [ ] `AI/AI_SDLC(pipeline).txt` — 최신 파이프라인 및 게이트 정책 반영
```

작업 완료 후 `/pr` 단계에서 체크 처리한다.

### 완료 기준

- 신규 스프린트는 반드시 파일 맨 끝에 추가
- 기존 완료된 스프린트 위로 삽입하지 않음

---

## 4. 검증 기준

이번 작업 완료 후 아래 조건을 만족해야 한다.

### 문서 정합성

- [ ] `SCHEMA.md` 예시 JSON에 구형 `in_progress` 상태가 없음
- [ ] `/done`은 BUILD_GATE만 담당한다고 문서화되어 있음
- [ ] `/pr`은 PR_SUMMARY와 완료 처리를 담당한다고 문서화되어 있음
- [ ] pipeline 문서의 단계와 실제 스킬 이름이 일치함

### 스킬 정합성

- [ ] `/plan` 생성 JSON에 `impact`, `steps`, `test_generated`, `review_completed` 필드가 포함됨
- [ ] `/impact`가 task JSON에 결과를 기록하도록 안내함
- [ ] `/pr`이 task JSON을 읽고 게이트 조건을 검사함
- [ ] `/done`에 PR 생성 관련 단계가 다시 생기지 않음

### 테스트

- [ ] `dotnet build PlatformA.sln` 오류 0개
- [ ] `dotnet test PlatformA.sln` 통과
- [ ] 스킬/문서 변경만 있다면 빌드/테스트가 불필요한지 명확히 PR에 적을 것

---

## 5. 주의사항

### 5.1 너무 강한 게이트 금지

초기 단계에서 모든 상황을 강제로 막으면 workflow가 불편해질 수 있다.

권장 정책:

```text
/done = 경고 중심
/pr   = 최종 게이트 중심
```

### 5.2 문서-only 작업은 예외 허용

`.md`, `.txt`, `.yml`, `.json`, `.claude/`만 변경된 경우에는 `/test-gen` 미실행을 허용한다.

단, `.claude/skills/*.md` 변경은 AI_SDLC에 영향을 주므로 `/review` 권장 경고를 출력할 수 있다.

### 5.3 DB 전환은 이번 작업에서 하지 않음

이번 작업은 JSON 기반 상태 저장을 강화하는 것이 목적이다.

MariaDB/PostgreSQL 도입은 Phase 3에서 별도 작업으로 진행한다.

---

## 6. 최종 기대 결과

이번 작업 후 PlatformA의 AI_SDLC는 다음 수준으로 개선되어야 한다.

```text
단계가 존재하는 파이프라인
→ 단계 이행 여부를 검사하는 파이프라인
```

즉, 단순히 `/test-gen`, `/review`, `/impact`가 있는 것이 아니라,  
`/pr` 단계에서 이 결과를 확인하고 위험한 작업을 막을 수 있어야 한다.

최종 목표는 다음이다.

```text
사람은 요구사항과 승인에 집중하고,
AI는 분석·구현·테스트·검증·PR 정리를 수행하며,
파이프라인은 AI가 빠뜨린 단계를 감지한다.
```
