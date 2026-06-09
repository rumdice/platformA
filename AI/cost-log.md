# AI 작업 비용 로그

추적 목적: Phase 3(PostgreSQL 기반 모니터링) 도입 전 기준선 파악.  
Phase 3 전환 시 이 파일을 `ai_model_runs` 테이블로 마이그레이션한다.

## 작업 규모 기준

| 코드 | 기준 |
|------|------|
| **S** | 1-2 파일 변경, 단일 태스크 |
| **M** | 3-10 파일 변경 또는 2-4 태스크 |
| **L** | 10+ 파일 변경 또는 5+ 태스크 |
| **XL** | 전체 서비스 추가/마이그레이션 수준 |

## 기록 방법

`/done` 스킬 실행 시 PR 생성 후 아래 테이블에 행을 추가한다.

---

## 비용 로그

| 날짜 | 스프린트 | 작업명 | 모델 | 규모 | duration_sec | consume_tokens | cache_tokens | 비고 |
|------|---------|-------|------|------|-------------|---------------|-------------|------|
| 2026-05-18 | #21 | AISDLCEnhancements | claude-sonnet-4-6 | L | 1569288 | 920589482 | 26706003 | PDF 갭 해소 5종 (스킬/테스트/구조화) |
| 2026-05-21 | #22 | AddPipelineSkills | claude-sonnet-4-6 | M | 1310089 | 839379987 | 22067928 | /impact·/requirement 스킬 추가, 상태 머신 6단계, /done 비용 자동 계산 |
| 2026-05-21 | #23 | RefactorSdlcSkills | claude-sonnet-4-6 | M | 1310090 | 839379987 | 22067928 | /start·/pr 신규, /done BUILD_GATE 분리, /requirement 경로·소스탐지 개선 |
| 2026-05-22 | #24 | AddTestGenSkill | claude-sonnet-4-6 | M | 1223691 | 777294761 | 18459508 | /test-gen 신규, /done 데드코드 제거, SCHEMA 갱신, /review 완료 처리 추가 |
| 2026-05-22 | #24 | FixTestWriterDoc | claude-sonnet-4-6 | S | 1223691 | 777294761 | 18459508 | test-writer.md — Ticketing/Matching 테스트 현황 반영 및 팩토리 패턴 추가 |
| 2026-05-22 | #26 | AddPrMergeSyncWorkflow | claude-sonnet-4-6 | M | 1223693 | 777294761 | 18459508 | feat: PR 머지 자동 감지 — GitHub Actions SDLC Task Sync 워크플로우 |
| 2026-05-26 | #27 | SdlcGateCheckAndReport | claude-sonnet-4-6 | M | 878094 | 727542478 | 16974328 | feat: GitHub Actions gate check 이중화 + 주간 리포트 스크립트 (PR #55) |
| 2026-05-26 | #28 | AddCostLogMetrics | claude-sonnet-4-6 | M | 2208 | 16096374 | 654245 | feat: cost-log 메트릭 강화 — duration_sec/consume_tokens/cache_tokens 자동 계산 (PR #56) |
| 2026-05-28 | #29 | DocAutomationImprovement | claude-sonnet-4-6 | L | 705296 | 707529073 | 15920192 | feat: 문서화 자동화 — api-guide·DB 스키마 자동 생성 + MySqlDB.Lib·Game.Server DocFX 추가 (PR #57) |
| 2026-05-29 | #30 | DotNet10Upgrade | claude-sonnet-4-6 | L | 618897 | 684500598 | 14870875 | upgrade: .NET 8/9 → 10 전체 TFM 통일, Pomelo 9.0.0, EF Core 9.x, .NET 10 breaking change 3건 수정 (PR #58) |
| 2026-05-30 | #30 | ModernizeNet10Stack | claude-sonnet-4-6 | L | 3192 | 98248083 | 1470418 | feat: OpenAPI 현대화(Swashbuckle→OpenApi+Scalar), C# primary constructors 6개, 스킬 python3→bash 수정 (PR #59) |
| 2026-05-30 | #30 | AutoUpdateDocMeta | claude-sonnet-4-6 | M | 722 | — | — | feat: 문서 메타데이터 자동화 — .NET 버전·테스트 수 csproj 동적 파싱, generate_doc_meta.py 신규 (PR #60) |
| 2026-06-02 | #32 | AutoDocProtoRedisKeys | claude-sonnet-4-6 | M | 273299 | 446964453 | 8495102 | feat: proto·Redis 키스페이스 문서 자동화 — generate_proto_docs.py·generate_redis_key_docs.py 신규 (PR #61) |
| 2026-06-02 | #33 | UpgradeToSystemThreadingLock | claude-sonnet-4-6 | S | 251700 | 304675268 | 6796731 | feat: System.Threading.Lock 전환 — object _lock 3곳 교체 및 스레드 안전성 테스트 8개 추가 (PR #62) |
| 2026-06-02 | #34 | FixCostLogCalcWindows | claude-sonnet-4-6 | S | 1556 | 7027484 | 83800 | fix: cost-log duration/token 계산 안정화 — date -d 제거, Python 단일 호출로 통합 (PR #63) |
| 2026-06-03 | #35 | SetupDockerOneClick | claude-sonnet-4-6 | M | 475 | 11804327 | 65899 | feat: Docker 원클릭 환경 구성 — setup 스크립트·DB 자동생성·EF 마이그레이션·.env 변수화·SQLite 볼륨·DummyClient profile (PR #64) |
| 2026-06-03 | #36 | AddSdlcDockerInfra | claude-sonnet-4-6 | S | 830 | 10483470 | 32436 | feat: AI_SDLC Phase 3 인프라 Docker 설치 — PostgreSQL 16 + n8n, setup 스크립트, .env.example (PR #65) |
| 2026-06-03 | #37 | FixRequirementEnforcement | claude-sonnet-4-6 | S | 1586 | 27779817 | 173949 | fix: /pr requirement 차단 강화 + sprint35·36 requirement 소급 기록 (PR #66) |
| 2026-06-03 | #38 | FixPrArchiveDateDep | claude-sonnet-4-6 | S | 178 | 5558096 | 9268 | fix: /pr 4.5단계 명세 파일 archived 날짜 의존성 제거 — TODAY → PlanName 기반 검색 (PR #67) |
| 2026-06-03 | #39 | RestructureDockerSdlcInfra | claude-sonnet-4-6 | L | 140527 | 181548368 | 4239985 | refactor: docker/sdlc 폴더를 n8n·postgresql 독립 폴더로 분리, full compose 통합 |
| 2026-06-03 | #40 | AddAdrAndImproveDesignReview | claude-sonnet-4-6 | M | 139189 | 181548368 | 4239985 | docs: ADR-008(n8n)·ADR-009(PostgreSQL) 소급 생성 + DESIGN_REVIEW 워크플로 개선 |
| 2026-06-03 | #41 | AddSdlcDbLib | claude-sonnet-4-6 | L | 71957 | 93357112 | 2361462 | feat: PlatformA.SdlcDB.Lib — AI_SDLC Phase 3 PostgreSQL EF Core 상태 저장소 MVP [risk:HIGH] |
| 2026-06-04 | #42 | AutomateCiFailureDetection | claude-sonnet-4-6 | M | 70008 | 93357112 | 2361462 | feat: CI 실패 자동 감지·기록·수정 파이프라인 (n8n + PostgreSQL 기반) [risk:LOW] |
| 2026-06-05 | #43 | StabilizeSdlcPhase3DataFlow | claude-sonnet-4-6 | L | 9586 | 89250601 | 1500220 | feat: AI_SDLC Phase 3 데이터 흐름 안정화 — ai_failures 중복 방지 및 task JSON DB 이전 [risk:HIGH] |
| 2026-06-05 | #44 | AutomateWorkflowPipeline | claude-sonnet-4-6 | L | 5716 | 48537737 | 1047014 | feat: 워크플로 완전 자동화 기반 구축 — /workflow 오케스트레이터 + 스킬 차단 요인 제거 [risk:LOW] |
| 2026-06-05 | #45 | CompletePhase3Automation | claude-sonnet-4-6 | L | 1166 | 13077872 | 259111 | feat: Phase3 자동화 완성 — 외부 트리거 + CI 자동 수정 루프 + cost-log 역산 인프라 [risk:LOW] |
| 2026-06-05 | #46 | AddModelRunsIntegration | claude-sonnet-4-6 | M | 1168 | 15469790 | 272980 | feat: /pr 완료 시 sdlc.ai_model_runs 자동 기록 — insert_model_run.py 신규 + /pr SKILL.md 4.2단계 추가 [risk:LOW] |
| 2026-06-05 | #47 | AddLlmRouter | claude-sonnet-4-6 | M | 1859 | 23429700 | 347374 | feat: LLM 라우터 — impact.risk 기반 Haiku/Sonnet/Opus 자동 선택 (get_task_risk.py 신규 + 워크플로우 2개 수정) [risk:LOW] |
| 2026-06-05 | #48 | PostgresPrimaryMigration | claude-sonnet-4-6 | L | 3027 | 33948347 | 490813 | feat: PostgreSQL primary 전환 — db_write.py 신규 + 7개 스킬 dual-write 추가 [risk:LOW] |
| 2026-06-08 | #49 | StabilizeSdlcPhase3Ops | claude-sonnet-4-6 | L | 1429 | 19208710 | 313568 | feat: Phase 3 운영 안정화 — AI/sprints/ 구조 도입, DB/JSON 정합성 검사, db_write 실패 로그, 정책 문서 3종 [risk:LOW] |
| 2026-06-08 | #50 | MigrateToDbPrimary | claude-sonnet-4-6 | M | 935 | 17218269 | 299202 | fix+feat: DB primary 전환(Phase B) — db_write.py sprint 버그수정, sprint 카운터 수정, backfill 12개, /pr 게이트 DB primary 전환, Phase B 선언 [risk:LOW] |
| 2026-06-10 | #51 | PrepareDbPrimaryPhaseC | claude-sonnet-4-6 | L | 885 | 13477505 | 289003 | fix+feat: Phase B 마무리 — step_name 버그수정, LEGACY exception, 백업정책, generate_cost_log_from_db.py 신규, Phase C 조건 문서화 [risk:LOW] |
