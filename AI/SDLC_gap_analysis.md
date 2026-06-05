# AI SDLC 워크플로 갭 분석 보고서

작성일: 2026-05-19  
최종 갱신: 2026-06-05 (스프린트 #43 완료 반영 — Phase 3 진행 중 ~55%)  
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
| `/plan` | 브랜치 생성 + SPRINT.md 업데이트 + task JSON 초기화 |
| `/requirement` | 요구사항 분석 → 구현 명세 파일 생성 + DESIGN_REVIEW (Sprint #22) |
| `/impact` | 변경 파일 위험도 분류 + 참조 관계 + 테스트 커버리지 (Sprint #22) |
| `/start` | task 상태 coding 전환 + 명세 파일 기반 작업 지시서 출력 (Sprint #23) |
| `/done` | 빌드·포맷·테스트 → push (format 검증 포함 Sprint #42) |
| `/pr` | PR 생성 + SPRINT 완료 체크 + cost-log 자동 기록 (Sprint #23) |
| `/test-gen` | 변경 코드 기반 xUnit 테스트 자동 생성 (Sprint #24) |
| `/review` | 코드 리뷰 보고서 생성 |
| `/qa-failure` | CI 실패 로그 분석 → BUILD/FORMAT/TEST 분류 → 수정 방향 제시 (Sprint #28) |
| `/workreport` | 일일 작업 리포트 자동 생성 (AI/workreport/) |
| `/doc-writer` | API 가이드 문서 자동 생성 |
| `/adr` | ADR(Architecture Decision Record) 작성 |
| `/simplify` | 복잡한 코드 단순화 리팩터링 |
| `/run-scenarios` | DummyClient 시나리오 검증 |
| `[auto-format.yml]` | CI format 실패 시 자동 fix 커밋 재실행 (Sprint #42) |
| `[n8n CI Monitor]` | GitHub API 폴링 → ai_failures INSERT, 중복 방지 (Sprint #42, #43) |
| `[mark_ci_failure.py]` | CI 실패 유형 분류 + task JSON last_error + PR 댓글 (Sprint #42) |
| `[migrate_tasks_to_postgres.py]` | task JSON → sdlc.ai_jobs/ai_job_steps 이전 (Sprint #43) |
| `[record_failure.py]` | ai_failures 수동 기록, GitHub identity + ON CONFLICT 지원 (Sprint #43) |

### 2.2 상태 추적 구조

```
AI/
├── SPRINT.md          # 사람이 읽는 스프린트 현황 (체크박스 형태)
├── cost-log.md        # 수동 비용 기록 (Phase 3 전 기준선)
├── tasks/
│   ├── SCHEMA.md      # JSON 스키마 정의
│   └── sprint{N}_{PlanName}.json   # 태스크별 상태 파일
└── adr/               # 아키텍처 결정 기록
```

**task JSON 필드**: sprint, task, branch, status, created_at, completed_at, pr_url, retry_count, last_error, artifacts

### 2.3 CI/CD

- **플랫폼**: GitHub Actions
- **파이프라인**: .NET 9 setup → restore → build → format check → test
- **트리거**: 모든 PR (push to any branch)
- **상태 피드백**: 실패 시 /qa-failure 스킬로 수동 분석

### 2.4 현재 방식의 특성

- **Human-driven**: 모든 작업은 사용자가 /plan, /done을 직접 실행
- **단일 AI 인스턴스**: Claude Code 한 개가 전체 파이프라인 담당
- **파일 기반 상태**: PostgreSQL 없이 JSON 파일로 상태 추적
- **선형 플로우**: 브랜치 → 구현 → 빌드/테스트 → PR
- **스프린트 누적**: 21개 스프린트, 각 스프린트가 단일 태스크

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
| **오케스트레이션** | n8n 자동 트리거 | 사용자 수동 실행 | △ n8n CI 감지·기록 완료 (Sprint #42, #43). AI Worker 트리거 미구현 |
| **상태 DB** | PostgreSQL 6개 테이블 | JSON 파일 | △ 4개 테이블 구현(ai_jobs/steps/failures/model_runs, Sprint #41, #43). JSON과 병행 운용 중 |
| **CI 실패 기록** | (PDF 없음) | 없음 | ✅ mark_ci_failure.py + n8n + ai_failures 중복 방지 (Sprint #42, #43) |
| **상태 머신** | 9단계 + 에러 분기 | 6단계 | 6단계 유지 (analyzing/coding/testing/done/failed/abandoned) |
| **비용 추적** | `ai_model_runs` 자동 기록 | cost-log.md 자동 기록 (파일 수 기반) | cost-log.md 유지 (ai_model_runs 스키마 존재하나 연동 없음) |
| **다중 워커** | Worker 분리 + 헬스비트 | 단일 Claude Code 인스턴스 | 동일 (단일 인스턴스) |
| **Approval Gate** | 고위험 작업 인간 승인 | 없음 | △ sdlc-gate-check.yml이 SDLC 공정 준수 자동 검사로 부분 대체 |
| **LLM 라우터** | 태스크 복잡도별 모델 선택 | 단일 모델 고정 | 동일 (미구현) |
| **보안** | 컨테이너 격리, 화이트리스트 | 없음 | 동일 (미구현) |
| **롤백** | 자동 롤백 (Phase 4) | git revert 수동 | 동일 (수동) |
| **모니터링** | 대시보드, 비용 알림 | 없음 | session-start.sh 미해결 실패 알림 추가 (Sprint #42) |

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
| Phase 3 | 자동화 인프라 구축 | 🔄 진행 중 (~55%) — PostgreSQL 인프라(Sprint #41), n8n CI 감지(Sprint #42), ai_failures 중복 방지 + task JSON 이전(Sprint #43). 미완: n8n→AI Worker 루프, LLM 라우터, 자동 머지 |

---

## 6. 개선 우선순위 및 로드맵

### 6.1 단기 개선 (현재 스프린트 수준, 1~2주)

| 우선순위 | 개선 항목 | 이유 | 상태 |
|---------|---------|------|------|
| P0 | `/impact` 스킬 추가 | 변경 범위 파악이 없어 실수로 넓은 영향 작업 시작하는 경우 발생 | ✅ 완료 (2026-05-21) |
| P1 | 상태 머신 단계 세분화 | 4단계로는 중간 실패 원인 구분 불가 → 6단계: analyzing/coding/testing/done/failed | ✅ 완료 (2026-05-21) |
| P2 | 비용 자동 기록 | `/done` 스킬에서 변경 파일 수 기반 규모 자동 계산 후 cost-log.md 업데이트 | ✅ 완료 (2026-05-21) |
| P3 | `/requirement` 스킬 추가 | 요구사항 → 구현 명세 변환을 AI가 보조하면 /plan 품질 향상 | ✅ 완료 (2026-05-21) |

### 6.2 중기 개선 (Phase 2 완료 기준, 1~2개월)

| 개선 항목 | 내용 |
|---------|------|
| **비용 대시보드** | cost-log.md → AI/reports/cost_summary.md 자동 생성 (주간 집계) |
| **영향 분석 자동화** | `/impact` 스킬이 git diff + grep으로 영향 파일 목록 자동 추출 |
| **리뷰 게이트 강화** | `/done` 전에 `/review` 결과를 강제로 보여주는 옵션 |
| **스킬 메트릭** | 각 스킬의 평균 소요 시간/성공률 추적 (task JSON에 duration 필드 추가) |

### 6.3 장기 개선 (Phase 3 — 자동화 오케스트레이션)

| 개선 항목 | PDF 대응 | 필요 인프라 |
|---------|---------|-----------|
| **PostgreSQL 상태 DB** | ai_jobs + ai_job_steps | PostgreSQL Docker |
| **자동 트리거** | n8n 오케스트레이션 | n8n + webhook |
| **비용 실시간 추적** | ai_model_runs 테이블 | Anthropic API usage 연동 |
| **LLM 라우터** | 태스크별 모델 선택 | haiku(간단)/sonnet(중간)/opus(복잡) |
| **Approval Gate** | WAITING_REVIEW 상태 | GitHub PR review required 설정 |

### 6.4 현실적 갭 우선순위 (가성비 기준)

```
즉시 적용 가능 (코드 변경 없음):
  → GitHub PR required review 설정으로 Approval Gate 대체
  → /done 실행 전 /review 결과 의무화 (CLAUDE.md 규칙 추가)

단기 스킬 추가 (1~2일):
  → /impact 스킬: git diff HEAD~1..HEAD + ripgrep으로 영향 파일 분석
  → task JSON에 duration 필드 추가 (created_at vs completed_at 차이)

중기 인프라 (2~4주):
  → cost-log.md를 CSV로 전환 + Python 집계 스크립트
  → PostgreSQL 도입 (Phase 3 본격화 시점에)
```

---

## 요약

**현재 워크플로는 PDF의 Phase 2 Pilot 수준 진입**. 핵심 CODE_FIX/TEST/QA 루프 완성 + PostgreSQL 상태 DB + n8n CI 감지 파이프라인 구동 중.

**Phase 2 완료 (Sprint #22~31, 2026-05-21~30)**: /impact·/requirement·/start·/pr·/test-gen 추가, 상태 머신 6단계, SDLC gate check 강화, ADR 연계, PR 머지 자동 동기화.

**Phase 3 진행 중 (~55%, Sprint #41~43, 2026-06-03~05)**:
- 완료: PostgreSQL SdlcDB.Lib 4개 테이블 (Sprint #41)
- 완료: n8n CI 실패 감지 + format 자동 수정 파이프라인 (Sprint #42)
- 완료: ai_failures 중복 방지(partial unique index) + task JSON DB 이전 (Sprint #43)

**남은 주요 갭 (Phase 3 미완)**:
① n8n → Claude API 피드백 루프 (실패 감지 → 자동 수정 → CI 재실행)
② build/test 실패 자동 수정 (format만 현재 자동화됨)
③ LLM 라우터 (복잡도별 Haiku/Sonnet/Opus 선택)
④ PostgreSQL primary source 전환 (현재 JSON과 병행)

**2026-06-05 갱신**: SDLC_gap_analysis.md 전면 재평가 완료.
