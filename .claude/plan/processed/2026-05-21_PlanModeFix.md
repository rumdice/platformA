# Plan: Sprint #21 완료 처리 + AI_SDLC Pipeline 순차 동작 검증

## Context

이 플랜은 AI_SDLC(pipeline).txt의 **0. USER_PLAN** 단계에 해당한다.
사용자가 plan mode에서 계획을 수립하면 파이프라인 순서대로 작업이 진행된다.

### 문제 1: Sprint #21 미완료 표시
`AI/SPRINT.md`의 스프린트 #21 체크박스가 모두 `[ ]`로 남아 있다.
실제로는 6개 항목 모두 이미 완료된 아티팩트가 존재함을 확인:
- ✅ `.claude/skills/qa-failure/SKILL.md` 존재
- ✅ `PlatformA.Tests.Matching.API/` 존재 (8 테스트 통과)
- ✅ `PlatformA.Tests.Ticketing.API/` 존재 (9 테스트 통과)
- ✅ `AI/tasks/SCHEMA.md` 존재
- ✅ `AI/cost-log.md` 존재
- ✅ 11개 SKILL.md 모두 `schema_version: 1` 보유
- `AI/tasks/sprint21_AISDLCEnhancements.json` status가 여전히 `in_progress`

### 문제 2: AI_SDLC Pipeline 동작 검증
구현된 파이프라인 스킬들(/requirement, /impact, /done)이 실제로 순서대로
올바르게 동작하는지 검증이 필요하다.

---

## 파이프라인 실행 계획

이 작업 자체가 파이프라인 테스트다. plan mode 승인 후 아래 순서로 진행한다.

### 0. USER_PLAN ✅ (현재 — plan mode)
이 계획 파일이 해당 단계.

### 1. REQUIREMENT_ANALYSIS (/requirement 스킬)
이 계획을 `/requirement` 스킬로 검토:
- 요구사항 누락/오류 체크
- 구현 명세 확정

### 2. DESIGN_REVIEW
단순 문서 수정 + 검증 작업 → ADR 불필요, 생략

### 3. IMPACT_ANALYSIS (/impact 스킬)
`/impact` 스킬 실행:
- 변경 대상: `AI/SPRINT.md`, `AI/tasks/sprint21_AISDLCEnhancements.json`
- 예상 위험도: 🟢 LOW (문서/JSON만 변경)

### 4. CODE_FIX
변경 파일 3개:

**`CLAUDE.md`**
- `## Plan 파일 정책` 섹션을 구 정책으로 교체:
  - 매 plan mode 시작 시 Claude가 `C:\Users\rumdi\.claude\plans\YYYY-MM-DD_PlanName.md` 신규 생성
  - 시스템 지정 임의 파일(`aws-wiggly-bee.md` 등)은 무시하고 CLAUDE.md 규칙 우선

**`AI/SPRINT.md`**
- `## 스프린트 #21` 섹션: `### 진행 중` → `### 완료`
- 6개 `- [ ]` → `- [x]` 변경

**`AI/tasks/sprint21_AISDLCEnhancements.json`**
- `"status": "in_progress"` → `"status": "done"`
- `"completed_at": null` → 완료 날짜 기입
- `"pr_url": null` → PR #43 URL 기입 (해당 브랜치 PR 확인 필요)

### 5. TEST_CASE_GENERATION
문서/JSON 전용 변경 → 테스트 코드 생성 불필요, 생략

### 6. BUILD_TEST
`dotnet build PlatformA.sln` + `dotnet test PlatformA.sln` 실행 (코드 무변경이므로 통과 예상)

### 7. QA_FAILURE_ANALYSIS
빌드/테스트 실패 시에만 실행

### 8. PR_SUMMARY (/done 스킬)
`/done` 실행으로 PR 생성

---

## 변경 파일

| 파일 | 변경 유형 | 내용 |
|------|---------|------|
| `CLAUDE.md` | 수정 | Plan 파일 정책 → 구 정책(YYYY-MM-DD) 복원 |
| `AI/SPRINT.md` | 수정 | 스프린트 #21 "진행 중" → "완료", `[ ]` → `[x]` |
| `AI/tasks/sprint21_AISDLCEnhancements.json` | 수정 | status done, completed_at, pr_url 업데이트 |

---

## 검증

- `AI/SPRINT.md`: 스프린트 #21 섹션이 `### 완료`이고 모든 항목이 `[x]`인지 확인
- `sprint21_AISDLCEnhancements.json`: `"status": "done"` 확인
- `dotnet test PlatformA.sln`: 113개 통과 확인
- PR 생성 후 CI 통과 확인
