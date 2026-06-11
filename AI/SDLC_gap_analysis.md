# AI SDLC 워크플로 갭 분석 보고서

작성일: 2026-05-19  
최종 갱신: 2026-06-11 (스프린트 #61 완료 반영 — Phase C 운영 중)  
작성 목적: 엔터프라이즈 AI SDLC 플랫폼 설계(PDF)와 현재 프로젝트 AI 워크플로 비교 → 개선 로드맵 수립

---

## 1. 엔터프라이즈 AI SDLC 플랫폼 분석 (PDF)

### 1.1 아키텍처 구성요소

| 레이어 | 기술 | 역할 |
|-------|------|------|
| 워크플로 오케스트레이션 | n8n | 태스크 큐 구독 → AI Worker 트리거 → 결과 수집 |
| AI 실행기 | AI Worker (OMO/OpenCode) | LLM 호출, 코드 수정, 명령어 실행 |
| 검증 | GoCD | 빌드·포맷·테스트 파이프라인 자동 실행 |
| 상태 저장 | PostgreSQL | 작업 상태, 단계별 이력, 비용 추적, 아티팩트 |
| 큐/캐시 | Redis | 작업 큐, 분산 락, Rate Limit, Worker 헬스비트 |
| 사람 검토 | Approval Gate | 고위험 작업 진행 전 인간 승인 요구 |

### 1.2 9가지 태스크 유형

| # | 태스크 유형 | 설명 |
|---|-----------|------|
| 1 | REQUIREMENT_ANALYSIS | 요구사항 분석 → 구현 명세 도출 |
| 2 | DESIGN_REVIEW | 설계 검토 → ADR 생성 |
| 3 | IMPACT_ANALYSIS | 변경 영향 범위 분석 |
| 4 | CODE_FIX | 코드 수정/구현 |
| 5 | TC_EXTRACTION | 테스트 케이스 추출 |
| 6 | TEST_CODE_GENERATION | 테스트 코드 자동 생성 |
| 7 | BUILD_TEST | 빌드·테스트 실행 검증 |
| 8 | QA_FAILURE_ANALYSIS | CI 실패 원인 분석 |
| 9 | CODE_REVIEW / PR_SUMMARY | 코드 리뷰 + PR 요약 |

### 1.3 상태 머신

```
PENDING → ANALYZING → DESIGNING → CODING → TESTING → QA_ANALYZING → WAITING_REVIEW → DONE
                                                  ↓                        ↓
                                          RETRY_CODING              HUMAN_REQUIRED
                                                  ↓
                                              FAILED
```

### 1.4 PostgreSQL 스키마

| 테이블 | 용도 |
|--------|------|
| `ai_jobs` | 작업 헤더 (상태, 브랜치, 담당자) |
| `ai_job_steps` | 단계별 실행 이력 (입력/출력/소요시간) |
| `ai_messages` | LLM 메시지 히스토리 |
| `ai_model_runs` | 모델별 호출 비용 추적 |
| `ai_artifacts` | 생성 파일/패치/리포트 |
| `ai_approvals` | 인간 승인 이력 |
| `ai_task_definitions` | 재사용 가능한 태스크 정의 |

### 1.5 보안 설계

- 저장소 화이트리스트 (허가된 repo만 접근)
- 명령어 화이트리스트 (위험 명령 실행 차단)
- 시크릿 마스킹 (로그에서 토큰·패스워드 제거)
- 컨테이너 격리 (Worker 간 파일시스템 분리)
- 감사 로그 (모든 AI 작업 추적 가능)

### 1.6 구현 로드맵

| 단계 | 기간 | 목표 |
|------|------|------|
| Phase 1 — PoC | 2~4주 | 단일 워커 + 로컬 PostgreSQL + 수동 트리거 |
| Phase 2 — Pilot | 1~2개월 | n8n 연동, CI 자동화, Rate Limit |
| Phase 3 — Platform | 3개월+ | 멀티 워커, 비용 대시보드, Approval Gate |
| Phase 4 — Advanced | 이후 | LLM 라우터, 자동 롤백, 보안 강화 |

---

## 2. 현재 프로젝트 AI 워크플로 분석

### 2.1 스킬 목록 (11종)

| 스킬 | 역할 |
|------|------|
| `/plan` | 브랜치 생성 + sprint-NNN.md 생성 + DB ai_jobs upsert (Phase C: task JSON 생성 없음) |
| `/requirement` | 요구사항 분석 → 구현 명세 파일 생성 + DESIGN_REVIEW (Sprint #22) |
| `/impact` | 변경 파일 위험도 분류 + 참조 관계 + 테스트 커버리지 (Sprint #22) |
| `/start` | task 상태 coding 전환 + 명세 파일 기반 작업 지시서 출력 (Sprint #23) |
| `/done` | 빌드·포맷·테스트 → push (format 검증 포함 Sprint #42) |
| `/pr` | PR 생성 + SPRINT 완료 체크 + DB 상태 갱신 (Phase C: cost-log 없음) (Sprint #23) |
| `/test-gen` | 변경 코드 기반 xUnit 테스트 자동 생성 (Sprint #24) |
| `/review` | 코드 리뷰 보고서 생성 |
| `/qa-failure` | CI 실패 로그 분석 → BUILD/FORMAT/TEST 분류 → 수정 방향 제시 (Sprint #28) |
| `/workreport` | 일일 작업 리포트 자동 생성 (AI/workreport/) |
| `/workflow` | plan→pr 전체 파이프라인 완전 자동화 오케스트레이터 (Sprint #44) |
| `/doc-writer` | API 가이드 문서 자동 생성 |
| `/adr` | ADR(Architecture Decision Record) 작성 |
| `/simplify` | 복잡한 코드 단순화 리팩터링 |
| `/run-scenarios` | DummyClient 시나리오 검증 |
| `[plan-file-trigger.yml]` | .claude/plan/*.md Push → /workflow 자동 실행 (Sprint #45) |
| `[auto-fix.yml]` | repository_dispatch(ai-auto-fix) → /qa-failure 자동 수정 (Sprint #45) |
| `[auto-format.yml]` | CI format 실패 시 자동 fix 커밋 재실행 (Sprint #42) |
| `[n8n CI Monitor]` | GitHub API 폴링 → ai_failures INSERT, fixable_by_ai 필터 + dispatch (Sprint #42, #43, #45) |
| `[mark_ci_failure.py]` | CI 실패 유형 분류 + task JSON last_error + PR 댓글 (Sprint #42) |
| `[migrate_tasks_to_postgres.py]` | task JSON → sdlc.ai_jobs/ai_job_steps 이전 (Sprint #43) |
| `[record_failure.py]` | ai_failures 수동 기록, GitHub identity + ON CONFLICT 지원 (Sprint #43) |
| `[backfill_cost_log.py]` | 누락 스프린트 토큰 역산 → cost-log.md 보정 (Sprint #45, Phase C 이후 사용 중단) |
| `[job_lock.py]` | DB 기반 분산 잠금 — 동시 실행 방지 (Sprint #57) |
| `[check_sdlc_consistency.py]` | PostgreSQL ↔ task JSON 정합성 검사 (Sprint #49) |

### 2.2 상태 추적 구조 (Phase C, 2026-06-10~)

```
AI/
├── sprints/
│   └── sprint-NNN.md       # 스프린트별 상세 (YAML frontmatter, git 파일)
├── tasks/
│   ├── SCHEMA.md            # JSON 스키마 정의 (Phase B 이전 참조용)
│   └── sprint{N}_{PlanName}.json   # Phase B 이전 태스크 상태 (신규 생성 중단)
└── adr/                     # 아키텍처 결정 기록

삭제됨 (2026-06-11, PR #92):
  AI/SPRINT.md    → AI/sprints/sprint-NNN.md + DB(sdlc.ai_jobs)로 대체
  AI/cost-log.md  → DB(sdlc.ai_model_runs) 기반 비용 추적으로 대체
```

**PostgreSQL sdlc 스키마 (단일 진실 공급원)**:
- `ai_jobs`: 작업 헤더 (branch, sprint, status, created_at 등)
- `ai_job_steps`: 단계별 실행 이력
- `ai_failures`: CI 실패 기록 (GitHub identity + ON CONFLICT 중복 방지)
- `ai_model_runs`: 모델별 토큰 비용 추적

**Phase C sprint-NNN.md frontmatter 필드**: sprint, title, branch, date, status, completed, pr

### 2.3 CI/CD

- **플랫폼**: GitHub Actions
- **파이프라인**: .NET 9 setup → restore → build → format check → test
- **트리거**: 모든 PR (push to any branch)
- **상태 피드백**: 실패 시 /qa-failure 스킬로 수동 분석

### 2.4 현재 방식의 특성

- **반자동화**: 수동(/plan, /done)과 자동(plan-file-trigger.yml, auto-fix.yml) 경로 병존
- **단일 AI 인스턴스**: Claude Code 한 개가 전체 파이프라인 담당 (n8n은 감시·트리거만)
- **DB 단독 상태 저장 (Phase C)**: PostgreSQL(ai_jobs/steps/failures/model_runs)이 단일 진실 공급원. task JSON 신규 생성 중단 (2026-06-10~).
- **선형 플로우**: 브랜치 → 구현 → 빌드/테스트 → PR
- **스프린트 누적**: 61개 스프린트 완료, 각 스프린트가 단일 태스크
- **GitHub Actions 제약**: 로컬 PostgreSQL 직접 접근 불가 (네트워크 분리). DB 작업 필요 시 n8n webhook 경유.

---

## 3~4. 비교 분석 및 갭 리포트

### 3.1 커버된 영역 (현재 구현 완료)

| PDF 태스크 유형 | 현재 스킬 | 커버 수준 |
|---------------|---------|---------|
| CODE_FIX | `/plan` + `/done` | ✅ 완전 커버 |
| TEST_CODE_GENERATION | `test-writer` 에이전트 | ✅ 완전 커버 |
| BUILD_TEST | `/done` 내 자동 실행 | ✅ 완전 커버 |
| QA_FAILURE_ANALYSIS | `/qa-failure` | ✅ 완전 커버 |
| CODE_REVIEW | `/review` | ✅ 완전 커버 |
| DESIGN_REVIEW | `/adr` | ✅ 부분 커버 (ADR만, 설계 검토 없음) |
| REQUIREMENT_ANALYSIS | `/requirement` | ✅ 완전 커버 (2026-05-21) |
| IMPACT_ANALYSIS | `/impact` | ✅ 완전 커버 (2026-05-21) |
| TC_EXTRACTION | — | ❌ 미구현 (test-writer가 직접 생성) |

### 3.2 아키텍처 갭

| 항목 | PDF 설계 | 현재 상태 | 갭 |
|------|---------|---------|-----|
| **오케스트레이션** | n8n 자동 트리거 | 사용자 수동 실행 | △ n8n CI 감지·기록 완료. plan-file-trigger.yml로 외부 계획 파일 → /workflow 자동 실행 구현 (Sprint #44, #45). n8n → AI Worker 직접 호출 루프 미구현 |
| **상태 DB** | PostgreSQL 6개 테이블 | DB 단독 (Phase C) | ✅ Phase B(Sprint #50): DB primary 선언. Phase C(2026-06-10~): task JSON 신규 생성 중단, DB 단독. 4개 테이블 구현(ai_jobs/steps/failures/model_runs). ai_messages·ai_artifacts·ai_approvals·ai_task_definitions 미구현. |
| **CI 실패 자동 수정** | (PDF 없음) | 없음 | ✅ n8n fixable_by_ai 필터 → repository_dispatch → auto-fix.yml → /qa-failure 완전 루프 (Sprint #42, #43, #45) |
| **내부 파이프라인 자동화** | (PDF 없음) | 없음 | ✅ /workflow 오케스트레이터: plan→pr 9단계 완전 자동화 (Sprint #44) |
| **상태 머신** | 9단계 + 에러 분기 | 6단계 | 6단계 유지 (analyzing/coding/testing/done/failed/abandoned) |
| **비용 추적** | `ai_model_runs` 자동 기록 | DB 기반 (Phase C) | ✅ ai_model_runs 연동 완료(Sprint #46). cost-log.md 삭제(2026-06-11, PR #92). DB 단독 비용 추적. |
| **다중 워커** | Worker 분리 + 헬스비트 | 단일 Claude Code 인스턴스 | 동일 (단일 인스턴스) |
| **Approval Gate** | 고위험 작업 인간 승인 | 없음 | △ sdlc-gate-check.yml이 SDLC 공정 준수 자동 검사로 부분 대체. PR 머지는 수동 유지 |
| **LLM 라우터** | 태스크 복잡도별 모델 선택 | 단일 모델 고정 | 동일 (미구현) |
| **보안** | 컨테이너 격리, 화이트리스트 | 없음 | 동일 (미구현) |
| **롤백** | 자동 롤백 (Phase 4) | git revert 수동 | 동일 (수동) |
| **모니터링** | 대시보드, 비용 알림 | 없음 | session-start.sh 미해결 실패 알림 (Sprint #42). 비용 대시보드 미구현 |

### 3.3 현재 워크플로의 강점

1. **실용성**: 복잡한 인프라 없이 Claude Code만으로 즉시 사용 가능
2. **스킬 체계**: 9개 스킬이 PDF의 주요 태스크 유형을 커버
3. **Git 통합**: 브랜치→PR→CI 흐름이 자연스럽게 연결됨
4. **반복 가능성**: SPRINT.md + task JSON으로 재현 가능한 워크플로
5. **규칙 기반**: CLAUDE.md + rules/ 폴더가 일관성 보장

---

## 5. 사용자 pipeline.txt 분석 및 현재 진행 상황

### 5.1 pipeline.txt 단계 구조

```
0. HUMAN_PLAN          → /plan 스킬 (구현 완료 ✅)
1. REQUIREMENT_ANALYSIS → /requirement 스킬 (구현 완료 ✅ 2026-05-21)
2. DESIGN_REVIEW       → /adr 스킬 (부분 구현 ⚠️)
3. IMPACT_ANALYSIS     → /impact 스킬 (구현 완료 ✅ 2026-05-21)
4. CODE_FIX            → /plan + /done (구현 완료 ✅)
5. TEST_CODE_GENERATION → test-writer 에이전트 (구현 완료 ✅)
6. QA_FAILURE_ANALYSIS  → /qa-failure (구현 완료 ✅)
7. PR_SUMMARY          → /done 내 PR 생성 (구현 완료 ✅)
```

### 5.2 Phase 진행 현황

| Phase | 목표 | 현재 상태 |
|-------|------|---------|
| Phase 1 | 모든 스킬/AI 문서 목적 명확화 | ✅ 완료 — CLAUDE.md, rules/, SPRINT.md, tasks/ 체계 수립 |
| Phase 2 | PDF 개선사항 적용, 가장 큰 갭 발견 | ✅ 완료 — Sprint #22~31: /impact·/requirement·/start·/pr·/test-gen 추가, 상태 머신 6단계, 비용 자동 기록, gate-check 강화 |
| Phase 3 | 자동화 인프라 구축 | ✅ 완료 (100%, Sprint #41~48, 2026-06-03~08) |
| Phase B | DB primary 전환 | ✅ 완료 (2026-06-08, Sprint #49~50) |
| Phase C | task JSON 폐기·DB 단독 운영 | 🔄 운영 중 (2026-06-10~, Sprint #51~61+) |

---

## 6. 개선 우선순위 및 로드맵

### 6.1 단기 개선 (현재 스프린트 수준, 1~2주)

| 우선순위 | 개선 항목 | 이유 | 상태 |
|---------|---------|------|------|
| P0 | `/impact` 스킬 추가 | 변경 범위 파악이 없어 실수로 넓은 영향 작업 시작하는 경우 발생 | ✅ 완료 (2026-05-21) |
| P1 | 상태 머신 단계 세분화 | 4단계로는 중간 실패 원인 구분 불가 → 6단계: analyzing/coding/testing/done/failed | ✅ 완료 (2026-05-21) |
| P2 | 비용 자동 기록 | `/done` 스킬에서 변경 파일 수 기반 규모 자동 계산 후 cost-log.md 업데이트 | ✅ 완료 (2026-05-21) |
| P3 | `/requirement` 스킬 추가 | 요구사항 → 구현 명세 변환을 AI가 보조하면 /plan 품질 향상 | ✅ 완료 (2026-05-21) |

### 6.2 Phase 3 잔여 항목 (~10%, 우선순위 순)

| 우선순위 | 개선 항목 | 내용 | 작업량 |
|---------|---------|------|--------|
| P1 | **ai_model_runs 연동** | /pr 스킬에 DB INSERT 추가 (cost-log.md 병행) → 비용 대시보드 가능 | S |
| P2 | **PostgreSQL primary 전환** | 스킬들이 파일 대신 DB 읽기/쓰기 (이중 기록 → DB 단독) | L |
| P3 | **LLM 라우터** | risk LOW→haiku, MEDIUM→sonnet, HIGH→opus 자동 선택 | M |
| P4 | ~~**PR 자동 머지**~~ | **영구 정책 제외** — PR 머지는 사람이 직접 검토·승인 후 수행 | — |

### 6.3 Phase 4 장기 개선 (자율 오케스트레이션)

| 개선 항목 | PDF 대응 | 필요 인프라 | 상태 |
|---------|---------|-----------|------|
| **n8n → Claude API 직접 호출** | n8n AI Worker 트리거 | n8n HTTP Request + Anthropic API | ❌ 미구현 |
| **비용 대시보드** | ai_model_runs 자동 기록 | PostgreSQL 쿼리 + 리포트 스크립트 | ❌ 미구현 |
| **다중 워커** | Worker 분리 + 헬스비트 | 컨테이너 분리 | ❌ 미구현 |
| **Approval Gate** | WAITING_REVIEW 상태 | GitHub Branch Protection 강화 | △ gate-check로 부분 대체 |
| **보안·롤백** | 컨테이너 격리, 자동 롤백 | Phase 4 인프라 | ❌ 미구현 |

### 6.4 현실적 우선순위 (가성비 기준, 2026-06-05 기준)

```
즉시 완료 가능 (1~2 스프린트):
  → ai_model_runs INSERT 추가 (/pr 스킬 1개 수정)
  (PR 자동 머지 — 정책으로 제외, 사람이 직접 수행)

중기 (3~5 스프린트):
  → PostgreSQL primary 전환 (스킬 5~6개 수정, 높은 ROI)
  → LLM 라우터 (task JSON risk level → 모델 선택)

장기 (Phase 4):
  → n8n → Claude API 직접 호출 루프 (아키텍처 전면 변경)
  → 비용 대시보드 (PostgreSQL 집계 쿼리 + 시각화)
```

---

## 요약

**현재 워크플로는 PDF의 Phase 3 Platform 수준 진입**. 내부 파이프라인 완전 자동화 + 외부 트리거 + CI 자동 수정 루프까지 구축 완료.

**Phase 2 완료 (Sprint #22~31, 2026-05-21~30)**: /impact·/requirement·/start·/pr·/test-gen 추가, 상태 머신 6단계, SDLC gate check 강화, ADR 연계, PR 머지 자동 동기화.

**Phase 3 완료 (100%, Sprint #41~48, 2026-06-03~08)**:
- ✅ PostgreSQL SdlcDB.Lib 4개 테이블 (Sprint #41)
- ✅ n8n CI 실패 감지 + format 자동 수정 파이프라인 (Sprint #42)
- ✅ ai_failures 중복 방지(partial unique index) + task JSON DB 이전 (Sprint #43)
- ✅ /workflow 오케스트레이터: plan→pr 9단계 완전 자동화 (Sprint #44)
- ✅ plan-file-trigger.yml: 외부 계획 파일 Push → /workflow 자동 실행 (Sprint #45)
- ✅ auto-fix.yml: fixable CI 실패 → /qa-failure 자동 수정 루프 (Sprint #45)
- ✅ ai_model_runs 연동: /pr 완료 시 PostgreSQL 비용 기록 (Sprint #46, PR #75)
- ✅ LLM 라우터: impact.risk 기반 Haiku/Sonnet/Opus 자동 선택 (Sprint #47, PR #76)
- ✅ PostgreSQL primary 전환 + 7개 스킬 dual-write (Sprint #48, PR #77)

**Phase B 완료 (Sprint #49~50, 2026-06-08)**:
- ✅ AI/sprints/ 구조 도입 — sprint-NNN.md 개별 관리, append-only 충돌 완화 (Sprint #49)
- ✅ DB primary 선언 — check_sdlc_consistency.py --strict 통과 (Sprint #50)

**Phase C 운영 중 (Sprint #51~, 2026-06-10~)**:
- ✅ Phase C 경화 — owner 자동 감지, localhost 접근 차단 (Sprint #55)
- ✅ Phase C Job Lock — DB 기반 동시 실행 제어 (Sprint #57)
- ✅ AI_SDLC 워크플로우 테스트 보강 (Sprint #58)
- ✅ DocFX AI_SDLC 섹션 분리 — Docs/ai-sdlc/ 신설 (Sprint #59)
- ✅ sync_merged_pr Phase C 오탐 수정 — sprint frontmatter 폴백 (Sprint #60)
- ✅ 역할 분담 명세 + plan-file-trigger 수정 — CLAUDE.md 역할 분담 표 (Sprint #61)
- ✅ AI/SPRINT.md, AI/cost-log.md 삭제 — DB+sprints/*.md 단일 진실 공급원 확정 (PR #92)

**운영 정책**:
① **PR 머지**: 항상 사람이 직접 검토·승인 후 수행 — 영구 정책 (자동 머지 미도입)
② **GitHub Actions**: 로컬 PostgreSQL 직접 접근 금지 — DB 작업 필요 시 n8n webhook 경유

---

## DB+n8n 기반 전환 로드맵

### 현재 구조 (파일 primary)
```
스킬 실행 → task JSON(파일) 읽기/쓰기 → 스킬 완료
                   ↓ (비동기, 별도 스크립트)
             ai_jobs / ai_job_steps (DB 미러)
```
n8n은 CI 실패 감지·기록 전용. 스킬 체인은 GitHub Actions(plan-file-trigger, auto-fix)가 담당.

### 전환 방향 (점진적, 권장)

**1단계 — 이중 기록 (단기, 1~2 스프린트)**
- `/pr` 스킬에 `ai_model_runs` INSERT 추가 (cost-log.md와 병행)
- `/plan` 스킬에 `ai_jobs` INSERT 추가 (task JSON 생성과 동시)
- 각 스킬 단계 완료 시 `ai_job_steps` INSERT 추가 (현재 task JSON steps[]와 동시)
- 이 단계까지는 스킬 실패 시 파일로 fallback 가능

**2단계 — DB primary 격상 (중기, 3~4 스프린트)**
- 스킬들이 task JSON 파일 대신 DB에서 상태를 읽도록 전환
- `migrate_tasks_to_postgres.py`가 이미 이전 로직을 구현 — 역방향 어댑터 참조 가능
- SPRINT.md는 DB 집계 쿼리로 생성하는 스크립트로 대체
- cost-log.md 의존성 제거

**3단계 — n8n 오케스트레이션 확장 (장기)**
- 현재: GitHub Actions가 `/workflow` 실행 (plan-file-trigger.yml)
- 목표: n8n이 `ai_jobs` 상태 변경을 감지 → Claude API 직접 호출 → 다음 단계 트리거
- 구현 방법: PostgreSQL LISTEN/NOTIFY 또는 n8n 폴링 → `/workflow` 대신 각 스킬 단계별 API 호출
- 이 단계에서 LLM 라우터(Haiku/Sonnet/Opus) 통합 가능

### 각 전환 단계의 비용-효과

| 단계 | 작업량 | 효과 | 위험도 |
|------|--------|------|--------|
| 1단계 이중 기록 | S (스킬 2~3개 수정) | ai_model_runs 연동, 비용 대시보드 가능 | LOW |
| 2단계 DB primary | L (스킬 전체 수정) | 파일 의존성 제거, DB 단일 진실원 | MEDIUM |
| 3단계 n8n 확장 | XL (아키텍처 변경) | 완전 자율 파이프라인 | HIGH |

**권장**: 1단계를 먼저 완료하여 ai_model_runs를 채우고 비용 대시보드를 구성한다. 2단계는 운용 안정성 확인 후 진행.

**2026-06-05 갱신**: Sprint #45 완료 기준 전면 재평가 완료.
