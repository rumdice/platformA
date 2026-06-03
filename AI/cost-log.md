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
| 2026-05-18 | #21 | AISDLCEnhancements | claude-sonnet-4-6 | L | — | — | — | PDF 갭 해소 5종 (스킬/테스트/구조화) |
| 2026-05-21 | #22 | AddPipelineSkills | claude-sonnet-4-6 | M | — | — | — | /impact·/requirement 스킬 추가, 상태 머신 6단계, /done 비용 자동 계산 |
| 2026-05-21 | #23 | RefactorSdlcSkills | claude-sonnet-4-6 | M | — | — | — | /start·/pr 신규, /done BUILD_GATE 분리, /requirement 경로·소스탐지 개선 |
| 2026-05-22 | #24 | AddTestGenSkill | claude-sonnet-4-6 | M | — | — | — | /test-gen 신규, /done 데드코드 제거, SCHEMA 갱신, /review 완료 처리 추가 |
| 2026-05-22 | #24 | FixTestWriterDoc | claude-sonnet-4-6 | S | — | — | — | test-writer.md — Ticketing/Matching 테스트 현황 반영 및 팩토리 패턴 추가 |
| 2026-05-22 | #26 | AddPrMergeSyncWorkflow | claude-sonnet-4-6 | M | — | — | — | feat: PR 머지 자동 감지 — GitHub Actions SDLC Task Sync 워크플로우 |
| 2026-05-26 | #27 | SdlcGateCheckAndReport | claude-sonnet-4-6 | M | — | — | — | feat: GitHub Actions gate check 이중화 + 주간 리포트 스크립트 (PR #55) |
| 2026-05-26 | #28 | AddCostLogMetrics | claude-sonnet-4-6 | M | 2208 | 16096374 | 654245 | feat: cost-log 메트릭 강화 — duration_sec/consume_tokens/cache_tokens 자동 계산 (PR #56) |
| 2026-05-28 | #29 | DocAutomationImprovement | claude-sonnet-4-6 | L | — | — | — | feat: 문서화 자동화 — api-guide·DB 스키마 자동 생성 + MySqlDB.Lib·Game.Server DocFX 추가 (PR #57) |
| 2026-05-29 | #30 | DotNet10Upgrade | claude-sonnet-4-6 | L | — | — | — | upgrade: .NET 8/9 → 10 전체 TFM 통일, Pomelo 9.0.0, EF Core 9.x, .NET 10 breaking change 3건 수정 (PR #58) |
| 2026-05-30 | #30 | ModernizeNet10Stack | claude-sonnet-4-6 | L | 3192 | 98248083 | 1470418 | feat: OpenAPI 현대화(Swashbuckle→OpenApi+Scalar), C# primary constructors 6개, 스킬 python3→bash 수정 (PR #59) |
| 2026-05-30 | #30 | AutoUpdateDocMeta | claude-sonnet-4-6 | M | 722 | — | — | feat: 문서 메타데이터 자동화 — .NET 버전·테스트 수 csproj 동적 파싱, generate_doc_meta.py 신규 (PR #60) |
| 2026-06-02 | #32 | AutoDocProtoRedisKeys | claude-sonnet-4-6 | M | — | — | — | feat: proto·Redis 키스페이스 문서 자동화 — generate_proto_docs.py·generate_redis_key_docs.py 신규 (PR #61) |
| 2026-06-02 | #33 | UpgradeToSystemThreadingLock | claude-sonnet-4-6 | S | — | — | — | feat: System.Threading.Lock 전환 — object _lock 3곳 교체 및 스레드 안전성 테스트 8개 추가 (PR #62) |
| 2026-06-02 | #34 | FixCostLogCalcWindows | claude-sonnet-4-6 | S | 1556 | 7027484 | 83800 | fix: cost-log duration/token 계산 안정화 — date -d 제거, Python 단일 호출로 통합 (PR #63) |
| 2026-06-03 | #35 | SetupDockerOneClick | claude-sonnet-4-6 | M | 475 | 11804327 | 65899 | feat: Docker 원클릭 환경 구성 — setup 스크립트·DB 자동생성·EF 마이그레이션·.env 변수화·SQLite 볼륨·DummyClient profile (PR #64) |
| 2026-06-03 | #36 | AddSdlcDockerInfra | claude-sonnet-4-6 | S | 830 | 10483470 | 32436 | feat: AI_SDLC Phase 3 인프라 Docker 설치 — PostgreSQL 16 + n8n, setup 스크립트, .env.example (PR #65) |
| 2026-06-03 | #37 | FixRequirementEnforcement | claude-sonnet-4-6 | S | 1586 | 27779817 | 173949 | fix: /pr requirement 차단 강화 + sprint35·36 requirement 소급 기록 (PR #66) |
| 2026-06-03 | #38 | FixPrArchiveDateDep | claude-sonnet-4-6 | S | 178 | 5558096 | 9268 | fix: /pr 4.5단계 명세 파일 archived 날짜 의존성 제거 — TODAY → PlanName 기반 검색 (PR #67) |
