# PLAN — 2026-05-23 AI_SDLC 고도화 작업 계획

작성일: 2026-05-23  
대상 프로젝트: PlatformA  
목적: 2026-05-22 기준 최신 AI_SDLC 개선 내용을 바탕으로, 다음 단계 작업을 Claude Code가 바로 수행할 수 있도록 구체적인 실행 계획으로 정리한다.

---

## 0. 현재 상태 요약

현재 PlatformA의 AI_SDLC는 다음 흐름으로 정리되어 있다.

```text
/requirement
→ /plan
→ /impact
→ /start
→ 코딩
→ /test-gen
→ /done
→ /review
→ /pr
→ PR merge
→ pr-merge-sync GitHub Actions
```

최근 완료된 주요 개선:

- `/test-gen` 추가로 `TEST_CASE_GENERATION` 단계 연결
- `/done` 책임을 BUILD_GATE로 축소
- `/pr`가 PR 생성, SPRINT 완료 체크, task JSON 완료, cost-log 기록 담당
- task JSON에 `test_generated`, `review_completed`, `impact`, `steps` 추가
- `/pr`에 test/review/impact 기반 게이트 검사 추가
- `/done`에 test-gen 미실행 경고 추가
- PR merge 후 `AI/tasks/*.json`, `AI/SPRINT.md`, `AI/cost-log.md`를 자동 동기화하는 GitHub Actions 추가
- `BOT_PUSH_TOKEN`을 사용하여 branch protection 환경에서도 merge sync가 작동하도록 수정

이번 작업의 목표는 새 기능을 무작정 추가하는 것이 아니라, 현재 생긴 AI_SDLC를 **실제 운영 가능한 자동화 파이프라인**으로 더 안정화하는 것이다.

---

## 1. 이번 작업의 핵심 목표

```text
AI_SDLC를 “스킬 기반 수동 파이프라인”에서
“GitHub Actions와 연결된 검증 가능한 파이프라인”으로 발전시킨다.
```

이를 위해 다음을 수행한다.

1. `/pr` 내부 게이트를 GitHub Required Check 수준으로 승격 준비
2. PR merge sync 워크플로우 안정화
3. task JSON의 `steps[]` 기록 강화
4. AI_SDLC 주간 리포트 생성 기반 마련
5. 제품 개발로 넘어가기 전 pipeline 신뢰성 확보

---

## 2. 작업 범위

### 포함

- `.github/workflows/` 수정 또는 신규 추가
- `.github/scripts/` Python 스크립트 추가/수정
- `AI/tasks/SCHEMA.md` 수정
- `.claude/skills/pr/SKILL.md` 수정
- `.claude/skills/done/SKILL.md` 수정 검토
- `AI/AI_SDLC(pipeline).txt` 수정
- `AI/SPRINT.md` 스프린트 추가
- `AI/cost-log.md` 정합성 검토
- `AI/reports/` 디렉토리 및 리포트 생성 스크립트 추가 검토

### 제외

- MariaDB/PostgreSQL 도입
- n8n 도입
- LLM Router 도입
- AI Worker 분리
- 실제 배포 자동화
- atomicUtils 제품 기능 구현
- 게임 서버 기능 구현

---

## 3. 작업 원칙

```text
1. AI_SDLC 자체를 과도하게 복잡하게 만들지 않는다.
2. 현재 JSON/Markdown 기반 상태 저장 구조를 유지한다.
3. GitHub Actions를 통해 사람이 빠뜨릴 수 있는 부분만 자동 보완한다.
4. 사람의 승인/머지 권한은 유지한다.
5. 자동화는 검증과 기록을 담당하고, 최종 의사결정은 사람이 한다.
```

---

# TASK 1. AI_SDLC Gate Check GitHub Actions 추가

## 목적

현재 `/pr` 스킬 내부에는 test/review/impact 기반 게이트가 존재한다.  
하지만 사용자가 `/pr`을 우회하거나 GitHub UI에서 직접 PR을 만들면 이 게이트를 우회할 수 있다.

따라서 PR 생성/업데이트 시 GitHub Actions가 동일한 조건을 검사하도록 한다.

## 신규 파일

```text
.github/workflows/sdlc-gate-check.yml
.github/scripts/check_sdlc_gate.py
```

## 동작 시점

```yaml
on:
  pull_request:
    types: [opened, synchronize, reopened, ready_for_review]
    branches: [main]
```

## 검사 대상

GitHub Actions는 PR의 head branch 이름으로 `AI/tasks/*.json`을 찾는다.

```text
BRANCH = github.event.pull_request.head.ref
```

## 검사 규칙

### 1. task JSON 존재 여부

- task JSON이 없으면 warning
- 실패 처리하지 않는다
- 이유: 긴급 핫픽스, 외부 PR, 문서 수정 등 예외가 있을 수 있음

### 2. 코드 변경 여부

PR 변경 파일 중 아래 확장자가 있으면 코드 변경으로 판단한다.

```text
.cs
.proto
.csproj
```

### 3. test_generated 검사

코드 변경이 있고, 다음 경로 중 하나가 변경되었는데 `test_generated == false`이면 실패 처리한다.

```text
PlatformA.*.API/
PlatformA.Game.Server/
PlatformA.Library/
*.proto
```

예외:

```text
문서, .claude, AI 문서, GitHub workflow만 변경된 경우 통과
```

### 4. impact 검사

코드 변경이 있는데 `impact == null`이면 warning으로 처리한다.

단, 아래 고위험 경로 변경인데 impact가 없으면 실패 처리한다.

```text
PlatformA.Library/
Migrations/
*DbContext*
Entities/
*Auth*
*Token*
*Jwt*
*Redis*
*Lock*
```

### 5. review_completed 검사

아래 조건 중 하나라도 만족하면 review가 필요하다.

```text
impact.risk == "HIGH"
변경 파일 수 > 10
PlatformA.Library/ 변경
DB/Migration/Entity 변경
Auth/Token/JWT 변경
Redis/Lock 관련 변경
```

이때 `review_completed == false`이면 실패 처리한다.

## 출력

GitHub Actions summary에 다음을 출력한다.

```text
AI_SDLC Gate Check

Branch:
Task file:
Code changed:
Impact risk:
test_generated:
review_completed:

Result:
- PASS / FAIL / WARNING
```

## 완료 기준

- PR 생성 시 자동으로 AI_SDLC gate check가 실행됨
- gate 실패 시 PR status check가 실패함
- 문서-only PR은 통과함
- task JSON이 없을 때는 실패가 아니라 warning 처리함

---

# TASK 2. `sync_merged_pr.py` 강화

## 목적

현재 PR merge 후 상태 동기화는 동작하지만, 아직 단순하다.

현재 동작:

```text
task JSON status=done
completed_at 설정
pr_url 설정
SPRINT.md 체크
cost-log 추가
```

개선 목표:

```text
merge_sync 단계도 steps[]에 기록한다.
cost-log 중복 추가를 방지한다.
task JSON이 없을 때 GitHub Actions summary에 명확한 warning을 남긴다.
```

## 수정 파일

```text
.github/scripts/sync_merged_pr.py
```

## 개선 사항

### 1. `steps[]`에 merge_sync 기록

task JSON 업데이트 시 아래 step을 추가한다.

```json
{
  "name": "merge_sync",
  "status": "done",
  "started_at": "2026-05-23T00:00:00Z",
  "completed_at": "2026-05-23T00:00:00Z",
  "summary": "PR #54 merged and SDLC state synchronized"
}
```

이미 동일 PR 번호의 merge_sync step이 있으면 중복 추가하지 않는다.

### 2. cost-log 중복 방지

현재 `status == done`이면 cost-log 추가를 skip한다.  
추가로 cost-log 내부에 동일 PR 제목 또는 동일 task name이 이미 있으면 중복 추가하지 않는다.

### 3. GitHub Actions summary 출력

`GITHUB_STEP_SUMMARY`가 있으면 다음을 기록한다.

```markdown
## PR Merge SDLC Sync

- Branch:
- PR:
- Task file:
- Task status before:
- Task status after:
- SPRINT updated:
- cost-log updated:
- Warnings:
```

### 4. task JSON 없는 경우 처리

task JSON이 없으면 workflow는 성공 처리한다.  
다만 summary에 warning을 남긴다.

```text
No task JSON found for branch. Skipped SDLC sync.
```

## 완료 기준

- PR merge sync 실행 시 `steps[]`에 merge 기록이 남음
- cost-log 중복 행이 생기지 않음
- Actions summary에서 동기화 결과를 볼 수 있음
- task JSON 없는 브랜치도 workflow 실패 없이 종료됨

---

# TASK 3. `AI/tasks/SCHEMA.md`를 GitHub Actions 연동 기준으로 갱신

## 목적

현재 task JSON 스키마는 스킬 중심이다.  
이제 GitHub Actions도 task JSON을 읽고 쓰기 시작했으므로, 이를 문서화해야 한다.

## 수정 파일

```text
AI/tasks/SCHEMA.md
```

## 추가할 내용

### 1. GitHub Actions 연동 섹션

```markdown
## GitHub Actions 연동

다음 워크플로우가 task JSON을 읽거나 갱신한다.

| Workflow | 역할 |
|---|---|
| `sdlc-gate-check.yml` | PR 생성/수정 시 test/review/impact 게이트 검사 |
| `pr-merge-sync.yml` | PR merge 후 status=done, completed_at, pr_url, SPRINT, cost-log 동기화 |
```

### 2. `steps[]` 권장 step 이름

```markdown
| step name | 실행 주체 |
|---|---|
| requirement | /requirement |
| plan | /plan |
| impact | /impact |
| start | /start |
| test_gen | /test-gen |
| done | /done |
| review | /review |
| pr | /pr |
| gate_check | GitHub Actions |
| merge_sync | GitHub Actions |
```

### 3. 상태 책임 정리

```text
/plan  → analyzing
/start → coding
/done  → testing 또는 failed
/pr    → PR 생성 및 수동 완료 처리
pr-merge-sync.yml → PR merge 후 최종 done 보정
```

## 완료 기준

- 스킬과 GitHub Actions가 task JSON을 어떻게 사용하는지 명확해야 함
- 향후 RDB 마이그레이션 기준 문서로 사용할 수 있어야 함

---

# TASK 4. AI_SDLC 주간 리포트 생성 스크립트 초안 추가

## 목적

아직 RDB를 도입하지 않고도 JSON/Markdown 기반으로 AI 작업 현황을 볼 수 있게 한다.

## 신규 파일

```text
.github/scripts/generate_sdlc_report.py
AI/reports/README.md
```

## 리포트 출력 위치

```text
AI/reports/weekly_YYYY-WNN.md
```

## 집계 항목

- 전체 task 수
- 완료 task 수
- 실패 task 수
- 진행 중 task 수
- S/M/L/XL 작업 비율
- `test_generated=false`인 완료 task 목록
- `review_completed=false`인 HIGH risk task 목록
- `impact == null`인 코드 변경 task 목록
- cost-log 기준 작업 규모 분포

## 이번 작업에서의 현실적 범위

자동 스케줄까지 연결하지 않아도 된다.  
우선 스크립트만 추가하고 수동 실행 가능하게 한다.

```bash
python3 .github/scripts/generate_sdlc_report.py
```

## 완료 기준

- 수동 실행 시 `AI/reports/weekly_YYYY-WNN.md` 생성
- task JSON과 cost-log를 읽어 요약 생성
- 데이터가 부족한 항목은 `N/A`로 표시
- 스크립트 실행 실패 시 원인을 명확히 출력

---

# TASK 5. `AI/AI_SDLC(pipeline).txt` 최신화

## 목적

현재 AI_SDLC는 스킬뿐 아니라 GitHub Actions도 포함하게 되었다.  
문서에 이를 반영한다.

## 수정 내용

최신 pipeline을 아래처럼 정리한다.

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
10. PR_GATE_CHECK            → GitHub Actions sdlc-gate-check.yml
11. MERGE_SYNC               → GitHub Actions pr-merge-sync.yml
```

## 추가 설명

```text
- /pr 내부 게이트는 로컬/Claude Code 실행 시 1차 방어선이다.
- sdlc-gate-check.yml은 GitHub PR 단계에서 2차 방어선이다.
- pr-merge-sync.yml은 PR merge 이후 상태 정합성을 맞추는 사후 동기화 단계다.
```

## 완료 기준

- pipeline 문서가 현재 실제 구조와 일치해야 함
- GitHub Actions가 AI_SDLC의 일부임을 명확히 설명해야 함

---

# TASK 6. `AI/SPRINT.md`에 2026-05-23 스프린트 추가

## 목적

이번 작업을 추적 가능하게 만든다.

## 추가 위치

`AI/SPRINT.md` 파일 맨 끝에 추가한다.

## 예시

```markdown
---

## 스프린트 #27 (2026-05-23 ~)
**목표**: AI_SDLC GitHub Actions Gate 강화 및 자동 리포트 기반 마련

### 진행 중

- [ ] `.github/workflows/sdlc-gate-check.yml` — PR 단계 AI_SDLC gate check 추가
- [ ] `.github/scripts/check_sdlc_gate.py` — task JSON 기반 test/review/impact 검사 구현
- [ ] `.github/scripts/sync_merged_pr.py` — merge_sync steps 기록 및 summary 출력 강화
- [ ] `AI/tasks/SCHEMA.md` — GitHub Actions 연동 및 steps[] 기준 문서화
- [ ] `.github/scripts/generate_sdlc_report.py` — 주간 SDLC 리포트 생성 스크립트 추가
- [ ] `AI/AI_SDLC(pipeline).txt` — GitHub Actions 포함 최신 pipeline 반영
```

## 완료 기준

- 새 스프린트는 반드시 파일 맨 끝에 추가
- 기존 스프린트 중간에 삽입하지 않음
- 작업 완료 후 `/pr` 또는 merge sync로 완료 체크되도록 함

---

# TASK 7. 검증

## 문서/스크립트 검증

다음 명령을 실행한다.

```bash
python3 .github/scripts/check_sdlc_gate.py --dry-run
python3 .github/scripts/generate_sdlc_report.py
```

`sync_merged_pr.py`는 GitHub Actions 환경변수가 필요하므로 dry-run 옵션 추가를 권장한다.

```bash
python3 .github/scripts/sync_merged_pr.py --dry-run
```

## 빌드/테스트

.cs 파일 변경이 없다면 빌드/테스트는 필수는 아니지만, 가능하면 실행한다.

```bash
cd PlatformA
dotnet build PlatformA.sln
dotnet test PlatformA.sln
```

## 완료 기준

- Python 스크립트 문법 오류 없음
- GitHub Actions YAML 문법 오류 없음
- 기존 AI_SDLC 스킬 설명과 충돌 없음
- PR 설명에 “문서/스크립트 변경이므로 .NET 코드 영향 없음” 또는 실제 빌드/테스트 결과를 명시

---

## 4. 위험 요소와 주의사항

### 4.1 너무 강한 게이트 금지

초기에는 일부 예외 상황이 있을 수 있다.

권장 정책:

```text
task JSON 없음 → warning
impact 없음 → 일반 작업은 warning, 고위험 경로는 fail
test_generated 없음 → 코드 변경 시 fail
review_completed 없음 → HIGH 위험일 때 fail
```

### 4.2 BOT_PUSH_TOKEN 보안

`BOT_PUSH_TOKEN`은 최소 권한으로 관리해야 한다.

권장:

- Fine-grained PAT 사용 검토
- Repository contents write 권한만 부여
- 만료일 설정
- 토큰 재발급 절차 문서화

이번 작업에서 토큰을 코드나 문서에 직접 기록하지 않는다.

### 4.3 GitHub Actions 무한 루프 주의

`pr-merge-sync.yml`이 main에 커밋을 push하면 다른 workflow가 실행될 수 있다.  
필요하면 commit message 또는 path filter로 무한 루프를 방지한다.

### 4.4 제품 개발 지연 주의

AI_SDLC 개선은 제품 개발을 위한 수단이다.  
이번 작업 이후에는 atomicUtils 또는 PlatformA 실제 기능 개발로 넘어가는 것을 권장한다.

---

## 5. 최종 기대 결과

이번 작업이 완료되면 PlatformA의 AI_SDLC는 다음 구조가 된다.

```text
Claude Code 스킬
  → 요구사항 분석
  → 영향도 분석
  → 구현
  → 테스트 생성
  → 빌드/테스트
  → 리뷰
  → PR 생성

GitHub Actions
  → PR 단계에서 AI_SDLC gate 검사
  → PR merge 후 상태 자동 동기화
  → 주간 리포트 생성 기반
```

최종적으로 달성하려는 방향은 다음이다.

```text
사람은 요구사항, 승인, 제품 방향에 집중한다.
AI는 구현과 검증을 수행한다.
GitHub Actions는 누락된 공정을 감지한다.
task JSON은 현재 상태를 저장한다.
나중에 RDB가 이 상태 저장을 대체한다.
```

---

## 6. 다음 단계 예고

이번 작업 이후 다음 단계는 AI_SDLC 자체가 아니라 실제 제품 개발에 적용하는 것이다.

우선순위 후보:

```text
1. atomicUtils ↔ PlatformA.Utils.API 실제 연동
2. Utils.API 배포 및 CORS/HTTPS 정리
3. 사용자-facing 기능 완성
4. Admin/로그/통계 기반 추가
5. 첫 수익화 기능 실험
```

AI_SDLC는 계속 개선하되, 목적은 실제 제품 개발 속도를 높이는 것이다.
