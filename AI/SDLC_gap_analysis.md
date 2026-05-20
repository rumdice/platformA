# AI SDLC 워크플로 갭 분석 보고서

작성일: 2026-05-19  
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

### 2.1 스킬 목록 (9종)

| 스킬 | 역할 |
|------|------|
| `/plan` | 브랜치 생성 + SPRINT.md 업데이트 + task JSON 초기화 |
| `/done` | 빌드·포맷·테스트 → push → PR 생성 + cost-log 기록 |
| `/review` | 코드 리뷰 보고서 생성 |
| `/simplify` | 복잡한 코드 단순화 리팩터링 |
| `/qa-failure` | CI 실패 로그 분석 → BUILD/FORMAT/TEST 분류 → 수정 방향 제시 |
| `/doc-writer` | API 가이드 문서 자동 생성 |
| `/adr` | ADR(Architecture Decision Record) 작성 |
| `/run-scenarios` | DummyClient 시나리오 검증 |
| `/clean-build` | dotnet clean → build 캐시 오류 해결 |

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
| REQUIREMENT_ANALYSIS | — | ❌ 미구현 |
| IMPACT_ANALYSIS | — | ❌ 미구현 |
| TC_EXTRACTION | — | ❌ 미구현 (test-writer가 직접 생성) |

### 3.2 아키텍처 갭

| 항목 | PDF 설계 | 현재 상태 | 갭 |
|------|---------|---------|-----|
| **오케스트레이션** | n8n 자동 트리거 | 사용자 수동 실행 | 자동화 없음 |
| **상태 DB** | PostgreSQL 6개 테이블 | JSON 파일 | 쿼리·집계 불가 |
| **상태 머신** | 9단계 + 에러 분기 | 4단계 (pending/in_progress/done/failed) | 중간 단계 없음 |
| **비용 추적** | `ai_model_runs` 자동 기록 | cost-log.md 수동 기록 | 실시간 추적 없음 |
| **다중 워커** | Worker 분리 + 헬스비트 | 단일 Claude Code 인스턴스 | 병렬 실행 없음 |
| **Approval Gate** | 고위험 작업 인간 승인 | 없음 (모두 수동) | 게이트 없음 |
| **LLM 라우터** | 태스크 복잡도별 모델 선택 | 단일 모델 고정 | 비용 최적화 없음 |
| **보안** | 컨테이너 격리, 화이트리스트 | 없음 | 보안 경계 없음 |
| **롤백** | 자동 롤백 (Phase 4) | git revert 수동 | 자동 복구 없음 |
| **모니터링** | 대시보드, 비용 알림 | 없음 | 가시성 없음 |

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
1. REQUIREMENT_ANALYSIS → 미구현 ❌
2. DESIGN_REVIEW       → /adr 스킬 (부분 구현 ⚠️)
3. IMPACT_ANALYSIS     → 미구현 ❌
4. CODE_FIX            → /plan + /done (구현 완료 ✅)
5. TEST_CODE_GENERATION → test-writer 에이전트 (구현 완료 ✅)
6. QA_FAILURE_ANALYSIS  → /qa-failure (구현 완료 ✅)
7. PR_SUMMARY          → /done 내 PR 생성 (구현 완료 ✅)
```

### 5.2 Phase 진행 현황

| Phase | 목표 | 현재 상태 |
|-------|------|---------|
| Phase 1 | 모든 스킬/AI 문서 목적 명확화 | ✅ 완료 — CLAUDE.md, rules/, SPRINT.md, tasks/ 체계 수립 |
| Phase 2 | PDF 개선사항 적용, 가장 큰 갭 발견 | 🔄 진행 중 — 갭 분석(이 문서), /qa-failure 추가, tests.md 업데이트 |
| Phase 3 | AI/Skill 프로세스 분석, 분리 준비 | ⬜ 미시작 — 상태 DB, 자동화 오케스트레이션 단계 |

---

## 6. 개선 우선순위 및 로드맵

### 6.1 단기 개선 (현재 스프린트 수준, 1~2주)

| 우선순위 | 개선 항목 | 이유 |
|---------|---------|------|
| P0 | `/impact` 스킬 추가 | 변경 범위 파악이 없어 실수로 넓은 영향 작업 시작하는 경우 발생 |
| P1 | 상태 머신 단계 세분화 | 현재 4단계로는 중간 실패 원인 구분 불가 → 5단계: pending/analyzing/coding/testing/done |
| P2 | 비용 자동 기록 | `/done` 스킬에서 token 사용량 자동 추출 후 cost-log.md 업데이트 |
| P3 | `/requirement` 스킬 추가 | 요구사항 → 구현 명세 변환을 AI가 보조하면 /plan 품질 향상 |

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

**현재 워크플로는 PDF의 Phase 1 PoC 수준을 상회**하며, 핵심 CODE_FIX/TEST/QA 루프는 완성됨.  
**가장 큰 갭 3가지**: ① 자동 오케스트레이션 없음, ② REQUIREMENT/IMPACT 분석 단계 없음, ③ 비용 자동 추적 없음.  
**pipeline.txt Phase 2의 핵심 액션**: `/impact` 스킬 추가 + 상태 머신 5단계로 세분화 + 비용 자동 기록.
