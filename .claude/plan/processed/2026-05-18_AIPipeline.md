# Plan: AI SDLC 강화 5종 — 스프린트 #21

## Context

AI_SDLC_platform.pdf 분석 결과 현재 프로젝트가 Phase 1 PoC 기준을 충족하고 있으나,
Phase 2 진입을 위해 5개 갭을 해소한다:
1. CI 실패 자동 분석 스킬 없음 (`/qa-failure`)
2. Matching.API / Ticketing.API 통합 테스트 없음
3. 작업 상태 구조화 미흡 (SPRINT.md만 있음)
4. AI 비용/토큰 추적 수단 없음
5. 스킬 프롬프트 버전 관리 없음

---

## 작업 1: `/qa-failure` 스킬 신규 추가

### 파일
- **생성**: `.claude/skills/qa-failure/SKILL.md`

### 구현 내용

```yaml
---
name: qa-failure
description: GitHub Actions CI 실패를 자동 분석하고 failure_type·fixable·recommended_fix를 보고한다. gh CLI로 실패 로그를 가져와 Build/Format/Test 유형으로 분류한다.
schema_version: 1
allowed-tools: Bash(gh *) Grep Read
---
```

**수행 순서:**
1. `gh run list --status failure -L 5 --json databaseId,name,conclusion,createdAt,headBranch,url` — 최근 실패 5개 조회
2. 가장 최근 실패 런 선택 (또는 `$ARGUMENTS`로 run-id 직접 지정)
3. `gh run view <id> --log-failed` — 실패 로그 추출
4. 실패 유형 분류:
   - `BUILD`: `error CS`, `MSB`, `Build FAILED` 키워드
   - `FORMAT`: `dotnet format`, `whitespace`, `style` 키워드
   - `TEST`: `Failed!`, `FAILED`, `xUnit` 키워드
5. 분석 보고:
   - `failure_type`: BUILD / FORMAT / TEST
   - `fixable_by_ai`: true/false (BUILD·FORMAT은 대부분 true, TEST는 케이스별)
   - `error_summary`: 핵심 오류 5줄 이내
   - `recommended_fix`: 구체적 수정 방향 (파일명·라인 포함)
6. fixable_by_ai=true이면 수정 여부 사용자에게 확인

---

## 작업 2: `PlatformA.Tests.Matching.API` 통합 테스트

### 사전 파악 사항
- Matching.API는 **net9.0** → 테스트 프로젝트도 net9.0
- `TokenManager.GenerateJwtToken(playerId)` 직접 사용 가능 (PlatformA.Library 참조)
- `Consts.SECRET_KEY` 기본값 `"YourSuperSecretKeyForPlatformAMSA!@#123"` — 별도 TestTokenHelper 불필요
- `GameMatchController` 의존: `GameMatchService` → Redis (ZADD/ZREM/ZRANK/ZCARD)
- `DbWebAppContext`: 매칭 성사 시 기록용 — 컨트롤러 단위에서 InMemory로 교체
- `IHostedService` (EngineService, GameMatchService): 제거하여 Redis 실제 연결 차단

### 파일
- **생성**: `PlatformA/PlatformA.Tests.Matching.API/PlatformA.Tests.Matching.API.csproj`
- **생성**: `PlatformA/PlatformA.Tests.Matching.API/Helpers/MatchingTestWebAppFactory.cs`
- **생성**: `PlatformA/PlatformA.Tests.Matching.API/GameMatchControllerTests.cs`
- **수정**: `PlatformA/PlatformA.sln` — `dotnet sln add` 로 프로젝트 등록

### .csproj 구성
```xml
<TargetFramework>net9.0</TargetFramework>
패키지: Microsoft.AspNetCore.Mvc.Testing 9.x
        Microsoft.EntityFrameworkCore.InMemory 9.x
        Microsoft.NET.Test.Sdk, Moq 4.x, xunit 2.x, xunit.runner.visualstudio
ProjectReference: PlatformA.Matching.API, PlatformA.Library, PlatformA.MySqlDB.Lib
```

### `MatchingTestWebAppFactory` 핵심 패턴
```csharp
// 1. DbWebAppContext → InMemory 교체
//    AddDbContextFactory 내 MySQL Options 제거 후 재등록
// 2. RedisManager → Reflection으로 MockRedis 주입
//    type.GetField("_redis", flags).SetValue(instance, MockRedis.Object);
//    type.GetField("_pipeline", flags).SetValue(instance, ResiliencePipeline.Empty);
// 3. IHostedService 전체 제거
//    services.Where(d => d.ServiceType == typeof(IHostedService)).ToList()
//            .ForEach(d => services.Remove(d));
// builder.UseEnvironment("Testing");
```

### Redis Mock 설정
```csharp
MockRedisDb.Setup(x => x.SortedSetAddAsync(...)).ReturnsAsync(true);
MockRedisDb.Setup(x => x.SortedSetRemoveAsync(...)).ReturnsAsync(true);
MockRedisDb.Setup(x => x.SortedSetRankAsync(...)).ReturnsAsync((long?)0);
MockRedisDb.Setup(x => x.SortedSetLengthAsync(...)).ReturnsAsync((long)5);
MockRedisDb.Setup(x => x.ScriptEvaluateAsync(...)).ReturnsAsync(RedisResult.Create(1L));
```

### 테스트 케이스 (`GameMatchControllerTests`)
| 메서드 | 조건 | 예상 결과 |
|--------|------|---------|
| `RequestMatch_ValidToken_Returns200` | `TokenManager.GenerateJwtToken(1)` | 200 + Message |
| `RequestMatch_NoToken_Returns401` | Authorization 헤더 없음 | 401 |
| `CancelMatch_ValidToken_PlayerInQueue_Returns200` | Mock ZREM → true | 200 |
| `CancelMatch_ValidToken_PlayerNotInQueue_Returns404` | Mock ZREM → false | 404 |
| `CancelMatch_NoToken_Returns401` | Authorization 헤더 없음 | 401 |
| `GetStatus_ValidToken_PlayerInQueue_Returns200_WithRankTotal` | ZRANK → 0, ZCARD → 5 | 200 + Rank=1, Total=5 |
| `GetStatus_ValidToken_PlayerNotInQueue_Returns404` | ZRANK → null | 404 |
| `GetStatus_NoToken_Returns401` | Authorization 헤더 없음 | 401 |

---

## 작업 3: `PlatformA.Tests.Ticketing.API` 통합 테스트

### 사전 파악 사항
- Ticketing.API는 **net8.0**
- `QueueController` 의존: `QueueService` → Redis only (DB 없음)
- `[RedisRateLimit("queue")]` 적용 → 테스트 팩토리에서 policy "queue"를 limit 1000으로 재등록
- `IHostedService` (QueueWorkerService): 제거

### 파일
- **생성**: `PlatformA/PlatformA.Tests.Ticketing.API/PlatformA.Tests.Ticketing.API.csproj`
- **생성**: `PlatformA/PlatformA.Tests.Ticketing.API/Helpers/TicketingTestWebAppFactory.cs`
- **생성**: `PlatformA/PlatformA.Tests.Ticketing.API/QueueControllerTests.cs`
- **수정**: `PlatformA/PlatformA.sln` — `dotnet sln add` 로 프로젝트 등록

### .csproj 구성
```xml
<TargetFramework>net8.0</TargetFramework>
패키지: Microsoft.AspNetCore.Mvc.Testing 8.x
        Microsoft.NET.Test.Sdk, Moq 4.x, xunit 2.x, xunit.runner.visualstudio
ProjectReference: PlatformA.Ticketing.API, PlatformA.Library
```

### `TicketingTestWebAppFactory` 핵심 패턴
```csharp
// 1. RedisManager → Reflection 주입 (Auth.API 동일)
// 2. RedisRateLimiterService → policy "queue" limit 1000으로 재등록
//    svc.AddPolicy("queue", permitLimit: 1000, window: TimeSpan.FromMinutes(1));
// 3. IHostedService (QueueWorkerService) 제거
// builder.UseEnvironment("Testing");
```

### Redis Mock 설정
```csharp
MockRedisDb.Setup(x => x.ScriptEvaluateAsync(...)).ReturnsAsync(RedisResult.Create(1L));
MockRedisDb.Setup(x => x.StringSetAsync(...)).ReturnsAsync(true);
// GetStatus 대기 중 시나리오
MockRedisDb.Setup(x => x.SortedSetRankAsync(...)).ReturnsAsync((long?)3);
MockRedisDb.Setup(x => x.KeyExistsAsync(...)).ReturnsAsync(false);
```

### 테스트 케이스 (`QueueControllerTests`)
| 메서드 | 조건 | 예상 결과 |
|--------|------|---------|
| `EnterQueue_ValidToken_Returns200` | `TokenManager.GenerateJwtToken(1)` | 200 |
| `EnterQueue_NoToken_Returns401` | Authorization 없음 | 401 |
| `GetStatus_ValidToken_Waiting_Returns200_WithRank` | ZRANK → 3, KeyExists → false | 200 + Rank=3 |
| `GetStatus_ValidToken_Active_Returns200` | KeyExists → true | 200 + Status="Active" |
| `GetStatus_NoToken_Returns401` | Authorization 없음 | 401 |
| `LeaveQueue_ValidToken_InQueue_Returns200` | ZREM Mock → true | 200 |
| `LeaveQueue_ValidToken_NotInQueue_Returns404` | ZREM → false, Active → false | 404 |
| `LeaveQueue_NoToken_Returns401` | Authorization 없음 | 401 |

---

## 작업 4: `AI/tasks/` 경량 작업 상태 구조

### 파일
- **생성**: `AI/tasks/SCHEMA.md` — JSON 스키마 및 규칙 정의
- **생성**: `AI/tasks/sprint21_AISDLCEnhancements.json` — 이번 스프린트 초기 예시
- **수정**: `.claude/skills/plan/SKILL.md` — 브랜치 생성 후 task JSON 초기화 단계 추가
- **수정**: `.claude/skills/done/SKILL.md` — PR 생성 후 task JSON 완료 처리 단계 추가

### JSON 스키마
```json
{
  "sprint": 21,
  "task": "AISDLCEnhancements",
  "branch": "2026-05-18_AISDLCEnhancements",
  "status": "in_progress",
  "created_at": "2026-05-18T10:00:00Z",
  "completed_at": null,
  "pr_url": null,
  "retry_count": 0,
  "last_error": null,
  "artifacts": []
}
```

파일 경로 규칙: `AI/tasks/sprint{N}_{PlanName}.json`
status 값: `pending` | `in_progress` | `done` | `failed`

### `/plan` 스킬 변경 (브랜치 생성 직후)
```bash
SPRINT_NUM=$(grep -c "^## 스프린트" AI/SPRINT.md 2>/dev/null || echo "0")
PLAN_NAME="$(echo $BRANCH | sed 's/^[0-9-]*_//')"
TASK_FILE="AI/tasks/sprint${SPRINT_NUM}_${PLAN_NAME}.json"
echo "{\"sprint\":${SPRINT_NUM},\"task\":\"${PLAN_NAME}\",\"branch\":\"${BRANCH}\",\"status\":\"in_progress\",\"created_at\":\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\",\"completed_at\":null,\"pr_url\":null,\"retry_count\":0,\"last_error\":null,\"artifacts\":[]}" > "$TASK_FILE"
```

### `/done` 스킬 변경 (PR 생성 후)
```bash
CURRENT_BRANCH=$(git branch --show-current)
TASK_FILE=$(grep -rl "\"branch\":\"${CURRENT_BRANCH}\"" AI/tasks/ 2>/dev/null | head -1)
if [ -n "$TASK_FILE" ]; then
  # status → done, completed_at → now, pr_url → PR URL
  python3 -c "
import json,sys
data=json.load(open('${TASK_FILE}'))
data['status']='done'
data['completed_at']='$(date -u +%Y-%m-%dT%H:%M:%SZ)'
data['pr_url']='${PR_URL}'
json.dump(data,open('${TASK_FILE}','w'),indent=2)
"
fi
```

---

## 작업 5: `AI/cost-log.md` 추가

### 파일
- **생성**: `AI/cost-log.md`

### 형식
```markdown
# AI 작업 비용 로그

추적 목적: Phase 3(PostgreSQL 기반 모니터링) 도입 전 기준선 파악.
규모 기준: S(1-2 files), M(3-10 files), L(10+ files 또는 5+ 태스크)

| 날짜 | 스프린트 | 작업명 | 모델 | 작업 규모 | 비고 |
|------|---------|-------|------|---------|------|
| 2026-05-18 | #21 | AISDLCEnhancements | claude-sonnet-4-6 | L | 5개 항목 |
```

`/done` 스킬에 cost-log.md 항목 추가 프롬프트 포함 (PR 생성 직후 단계).

---

## 작업 6: 모든 `SKILL.md`에 `schema_version: 1` 추가

### 수정 파일 (8개)
frontmatter의 `name:` 바로 다음 줄에 `schema_version: 1` 삽입:

1. `.claude/skills/adr/SKILL.md`
2. `.claude/skills/build-check/SKILL.md`
3. `.claude/skills/doc-writer/SKILL.md`
4. `.claude/skills/done/SKILL.md`
5. `.claude/skills/plan/SKILL.md`
6. `.claude/skills/review/SKILL.md`
7. `.claude/skills/run-scenarios/SKILL.md`
8. `.claude/skills/simplify/SKILL.md`

---

## SPRINT.md 및 tests.md 업데이트

### `AI/SPRINT.md` 말미에 추가 (스프린트 #21)
```markdown
## 스프린트 #21 (2026-05-18~)
**목표**: AI SDLC 강화 5종 (PDF 갭 해소)

### 진행 중
- [ ] `.claude/skills/qa-failure/SKILL.md` — /qa-failure 스킬 신규 추가
- [ ] `PlatformA.Tests.Matching.API` — GameMatchController 통합 테스트 (8케이스)
- [ ] `PlatformA.Tests.Ticketing.API` — QueueController 통합 테스트 (8케이스)
- [ ] `AI/tasks/` — 경량 작업 상태 구조 + /plan & /done 스킬 연동
- [ ] `AI/cost-log.md` — AI 작업 비용 추적 로그 신규 추가
- [ ] 8개 SKILL.md — schema_version: 1 추가
```

### `.claude/rules/tests.md` 업데이트
"신규 테스트 작성 시" 현황 테이블에 Matching.API, Ticketing.API 행 추가

---

## 검증 절차

```bash
# 1. 솔루션에 두 프로젝트 추가
cd PlatformA
dotnet sln add PlatformA.Tests.Matching.API/PlatformA.Tests.Matching.API.csproj
dotnet sln add PlatformA.Tests.Ticketing.API/PlatformA.Tests.Ticketing.API.csproj

# 2. 빌드 — 0 오류 확인
dotnet build PlatformA.sln

# 3. 전체 테스트 — 신규 16개 포함 통과 확인
dotnet test PlatformA.sln

# 4. /qa-failure 스킬 동작 확인
# 최근 실패한 CI 런이 있을 경우 /qa-failure 실행하여 분석 보고 확인

# 5. AI/tasks/ 구조 확인
ls AI/tasks/
cat AI/tasks/sprint21_AISDLCEnhancements.json

# 6. SKILL.md schema_version 확인
grep -r "schema_version" .claude/skills/
```

---

## 변경 대상 파일 전체 목록

| 파일 | 유형 |
|------|------|
| `.claude/skills/qa-failure/SKILL.md` | 신규 |
| `PlatformA/PlatformA.Tests.Matching.API/PlatformA.Tests.Matching.API.csproj` | 신규 |
| `PlatformA/PlatformA.Tests.Matching.API/Helpers/MatchingTestWebAppFactory.cs` | 신규 |
| `PlatformA/PlatformA.Tests.Matching.API/GameMatchControllerTests.cs` | 신규 |
| `PlatformA/PlatformA.Tests.Ticketing.API/PlatformA.Tests.Ticketing.API.csproj` | 신규 |
| `PlatformA/PlatformA.Tests.Ticketing.API/Helpers/TicketingTestWebAppFactory.cs` | 신규 |
| `PlatformA/PlatformA.Tests.Ticketing.API/QueueControllerTests.cs` | 신규 |
| `AI/tasks/SCHEMA.md` | 신규 |
| `AI/tasks/sprint21_AISDLCEnhancements.json` | 신규 |
| `AI/cost-log.md` | 신규 |
| `.claude/skills/plan/SKILL.md` | 수정 (task JSON 생성 단계) |
| `.claude/skills/done/SKILL.md` | 수정 (task JSON 완료 + cost-log 기록) |
| `.claude/skills/adr/SKILL.md` | 수정 (schema_version) |
| `.claude/skills/build-check/SKILL.md` | 수정 |
| `.claude/skills/doc-writer/SKILL.md` | 수정 |
| `.claude/skills/review/SKILL.md` | 수정 |
| `.claude/skills/run-scenarios/SKILL.md` | 수정 |
| `.claude/skills/simplify/SKILL.md` | 수정 |
| `PlatformA/PlatformA.sln` | 수정 (sln add 2개) |
| `AI/SPRINT.md` | 수정 (스프린트 #21 추가) |
| `.claude/rules/tests.md` | 수정 (테스트 현황 테이블) |
