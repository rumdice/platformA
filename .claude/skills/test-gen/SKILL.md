---
name: test-gen
schema_version: 1
description: CODE_FIX 완료 후 테스트 케이스를 작성한다. 변경된 Controller/DTO/Service를 분석하여 test-writer 에이전트를 sub-agent로 실행하고, task JSON에 test_generated 필드를 기록한다. /start 이후 /done 이전에 실행한다. TEST_CASE_GENERATION 단계를 담당한다.
allowed-tools: Bash(git *) Bash(ls *) Bash(grep *) Bash(dotnet *) Read Edit
---

# 테스트 케이스 생성 (TEST_CASE_GENERATION)

## 컨텍스트
- 현재 브랜치: !`git branch --show-current`
- main 대비 변경 파일: !`git diff --name-only origin/main...HEAD 2>/dev/null || echo "(없음)"`
- 미커밋 변경 파일: !`git diff --name-only; git diff --name-only --cached`

---

## 헬퍼: task JSON 조회

```bash
CURRENT_BRANCH=$(git branch --show-current)
TASK_FILE=$(grep -rl "\"branch\": \"${CURRENT_BRANCH}\"" AI/tasks/ 2>/dev/null | head -1)
```

---

## 수행 순서

### 사전 검사

현재 브랜치가 `main`이면 즉시 중단한다:
> "main 브랜치에서는 /test-gen을 실행할 수 없습니다."

---

### 1단계: 테스트 대상 파일 수집

아래 bash로 변경 파일 전체를 수집한다:

```bash
git diff --name-only origin/main...HEAD 2>/dev/null
git diff --name-only
git diff --name-only --cached
```

수집된 파일에서 아래 패턴에 해당하는 파일만 테스트 대상으로 분류한다:

| 패턴 | 테스트 유형 |
|------|-----------|
| `*/Controllers/*Controller.cs` | 통합 테스트 (컨트롤러) |
| `*Request.cs`, `*Response.cs` | DTO DataAnnotation 유닛 테스트 |
| `*/Services/*Service.cs` | 외부 의존성 있으면 Mock, 없으면 순수 유닛 |
| 유틸리티 클래스 (`*Converter.cs`, `*Generator.cs` 등) | 순수 유닛 테스트 |

변경 파일이 `.md`, `.json`, `.yml`, `.txt`, `.claude/` 경로만이면:
> "테스트 생성이 필요한 소스 코드 변경이 없습니다." 출력 후 중단.

---

### 2단계: 명세 파일에서 검증 기준 추출

현재 브랜치의 PlanName으로 명세 파일을 탐색한다:

```bash
PLAN_NAME=$(git branch --show-current | sed 's/^[0-9-]*_//')
ls .claude/plan/processed/ 2>/dev/null | grep -i "${PLAN_NAME}" | tail -1
```

명세 파일이 있으면 Read 도구로 읽어 "검증 기준" 섹션을 추출한다.
없으면 변경 파일 분석만으로 진행한다.

---

### 3단계: 기존 테스트 커버리지 확인

```bash
ls PlatformA/PlatformA.Tests.*/ 2>/dev/null
```

대상 파일에 대응하는 테스트 프로젝트와 테스트 파일이 이미 있는지 확인한다.

| 서비스 | 테스트 프로젝트 | Redis 패턴 |
|--------|--------------|-----------|
| Auth.API | PlatformA.Tests.Auth.API | Reflection 주입 |
| Utils.API | PlatformA.Tests.Utils.API | 직접 교체 |
| Ticketing.API | PlatformA.Tests.Ticketing.API | Reflection 주입 |
| Matching.API | PlatformA.Tests.Matching.API | Reflection 주입 |
| Game.Server | PlatformA.Tests.Game.Server | — |

---

### 4단계: test-writer 에이전트 실행

아래 지시로 test-writer 에이전트를 sub-agent로 실행한다:

---
**test-writer 에이전트 지시:**

`.claude/agents/test-writer.md`의 지침에 따라 아래 대상의 xUnit 테스트를 생성하거나 보완하라.

**대상 파일:**
{1단계에서 분류된 테스트 대상 파일 — 절대경로 목록}

**기존 커버리지:**
{3단계에서 확인한 기존 테스트 파일 목록 (있으면)}
{신규 생성이 필요한 파일 목록}

**명세 검증 기준:**
{2단계에서 추출한 검증 기준 — 없으면 "없음"}

**규칙:**
- 기존 테스트 프로젝트가 있으면 기존 팩토리 패턴을 정확히 재사용한다 (`.claude/rules/tests.md` 참조)
- 기존 프로젝트가 없으면 test-writer 지침의 "신규 테스트 프로젝트 생성" 절차를 따른다
- 완료 후 `dotnet test PlatformA.sln -q`로 통과 확인한다

---

### 5단계: task JSON 갱신

TASK_FILE이 있으면 Edit 도구로 아래 두 가지를 갱신한다:

1. `"test_generated": false` → `"test_generated": true`

2. `steps[]` 배열에 아래 항목을 추가한다:
```json
{
  "name": "test_gen",
  "status": "done",
  "completed_at": "{ISO8601 현재 시각}",
  "summary": "{결과}: {테스트 대상 파일 수}개 파일 분석, {생성 완료/불필요} 판정"
}
```

없으면 이 단계를 건너뛴다.

---

### 6단계: 완료 보고

```
테스트 생성 완료 — {PlanName}

test_generated: true (task JSON 갱신)

대상:
  {테스트 대상 파일 목록}

생성/수정된 테스트:
  {파일 목록}

다음 단계:
  /done  — 빌드·테스트 검증 및 push
```
