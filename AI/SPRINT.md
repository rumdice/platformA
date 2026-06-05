# SPRINT — 현재 스프린트

> AI는 세션 시작 시 이 파일을 가장 먼저 읽습니다.
> 작업 완료 즉시 체크박스를 업데이트하십시오.

---

## 스프린트 #1 (2026-04-21 ~)
**목표**: AI 자율 개발 기반 문서 체계 구축 및 코드베이스 안정화

---

## 진행 중

(없음)

---

## 완료

- [x] CLAUDE.md 작성 (AI 운영 지침서)
- [x] docs/ARCHITECTURE.md 작성 (시스템 설계 문서)
- [x] docs/adr/001-redis-cluster.md 작성
- [x] docs/adr/002-binary-packet-protocol.md 작성
- [x] docs/adr/003-hardcoded-config.md 작성
- [x] docs/RUNBOOK.md 작성 (빌드/배포 명령)
- [x] docs/ENVIRONMENT.md 작성 (환경 설정)
- [x] docs/API_CONTRACTS.md 작성 (API 명세)
- [x] docs/DOMAIN.md 작성 (비즈니스 규칙)
- [x] docs/TESTING_STRATEGY.md 작성 (테스트 전략)
- [x] docs/PATTERNS.md 작성 (코딩 패턴)
- [x] docs/SPRINT.md + docs/BACKLOG.md 작성

---

## 대기

- [ ] `dotnet build PlatformA.sln` 빌드 오류 없음 최종 검증
- [ ] BACKLOG 항목 중 우선순위 합의 (#BACK-001 ~ #BACK-006)

---

## 스프린트 #2 (2026-04-22 ~)
**목표**: Utils.API 유닛 테스트 자동화 구축 및 검증

### 완료

- [x] Utils.API 유닛 테스트 명세 작성 (`AI/TESTING_STRATEGY.md` 업데이트)
- [x] 테스트 프로젝트 설정 수정 (FactAttribute 버그 제거, 패키지 추가)
- [x] `Base62ConverterTests` 작성 및 통과
- [x] `SnowflakeGeneratorTests` 작성 및 통과
- [x] `UtilControllerTests` (통합) 작성 및 통과
- [x] `dotnet test` 전체 통과 확인
- [x] GitHub Actions CI 파이프라인 구축 (빌드+테스트 자동 검증)
- [x] CI 트리거 범위 확장 (모든 PR 대상)
- [x] SessionStart 훅 추가 (세션 시작 시 SPRINT 현황 + 빌드 상태 자동 출력)

---

## 스프린트 #3 (2026-04-23 ~)
**목표**: Auth.API 유닛/통합 테스트 프로젝트 구축 및 CI 반영

### 완료

- [x] `AI/TESTING_STRATEGY.md` — Auth.API 테스트 명세 추가
- [x] `PlatformA.Tests.Auth.API` 프로젝트 생성 (csproj + 솔루션 등록)
- [x] `AuthTestWebAppFactory` 구현 (Redis Mock + InMemory DB + 고한도 Rate Limit)
- [x] `AuthControllerTests` 구현 (Login · Refresh · Logout 통합 테스트)
- [x] `AuthModelValidationTests` 구현 (DTO DataAnnotation 유닛 테스트)
- [x] `dotnet build PlatformA.sln` 오류 0개 확인
- [x] `dotnet test PlatformA.sln` 실패 0개 확인
- [x] push + PR + CI 통과

---

## 스프린트 #4 (2026-04-25 ~)
**목표**: Claude Code 커스텀 스킬 도입 및 로컬/웹 워크플로 통일

### 완료

- [x] 프로젝트 전용 커스텀 스킬 추가 (review, security-review, simplify, build-check, done)
- [x] 로컬/웹 환경 Git 워크플로 통일 (Plan 모드 → /done)
- [x] hooks 동적 경로 수정 (git rev-parse 기반, 로컬/웹 모두 동작)
- [x] settings.json 훅 상대경로 및 권한 정비
- [x] CLAUDE.md 브랜치 전략 재정의 및 절대경로 제거

---

## 스프린트 #5 (2026-04-27 ~)
**목표**: 민감 설정값 환경변수 이전 (BACK-001, BACK-009)

### 완료

- [x] `Consts.cs` — SECRET_KEY, MYSQL 연결 문자열을 환경변수 fallback 패턴으로 교체
- [x] `Utils.API/Program.cs` — Snowflake WorkerId 환경변수 주입
- [x] `AI/adr/004-env-var-config.md` 작성
- [x] `dotnet build PlatformA.sln` 오류 0개 확인
- [x] `dotnet test PlatformA.sln` 실패 0개 확인 (Utils 29 + Auth 19 = 48개 통과)
- [x] push + PR + CI 통과

---

## 스프린트 #6 (2026-04-29 ~)
**목표**: 개발 편의 커맨드 4종 추가 (Commands vs Skills 체계 정립)

### 완료

- [x] `/sprint` 커맨드 추가 (`.claude/commands/sprint.md`) — 스프린트/백로그 온디맨드 조회
- [x] `/clean-build` 스킬 추가 (`.claude/skills/clean-build/SKILL.md`) — MSB3492 캐시 오류 해결
- [x] `/migrate` 스킬 추가 (`.claude/skills/migrate/SKILL.md`) — EF Core 마이그레이션 가이드
- [x] `/adr` 스킬 추가 (`.claude/skills/adr/SKILL.md`) — ADR 자동 채번 + 템플릿 생성

---

## 스프린트 #7 (2026-04-30 ~)
**목표**: VS Code 빌드 환경 구성 (Visual Studio 동등 환경)

### 완료

- [x] `.vscode/tasks.json` 작성 — 빌드/릴리즈/클린/테스트/퍼블리시/Redis 태스크
- [x] `.vscode/launch.json` 작성 — 6개 프로젝트 디버그 실행 + 전체 API 동시 실행 compound
- [x] `.vscode/settings.json` 작성 — dotnet 기본 솔루션 지정
- [x] `dotnet build PlatformA.sln` 디버그/릴리즈 빌드 검증 (오류 0개)
- [x] `dotnet test PlatformA.sln` 전체 테스트 검증 (48개 통과)

---

## 스프린트 #8 (2026-04-30 ~)
**목표**: Claude 커스텀 Sub-Agent 도입 — test-writer 에이전트 추가

### 완료

- [x] `.claude/agents/test-writer.md` 작성 — xUnit 통합/유닛 테스트 자동 생성 에이전트

---

## 스프린트 #9 (2026-04-30 ~)
**목표**: /plan 워크플로 개선 — PR 머지 확인 및 당일 통합 브랜치 카운터 도입

### 완료

- [x] `/plan` 사전 검사 강화 — PR MERGED/OPEN/없음 케이스별 처리 (`.claude/skills/plan/SKILL.md`)
- [x] N 카운터 변경 — PlanName별 독립 카운터 → 당일 전체 통합 카운터
- [x] `CLAUDE.md` 업데이트 — 브랜치 네이밍 규칙 및 /plan 사전 검사 표 추가

---

## 스프린트 #10 (2026-04-30 ~)
**목표**: 코드 포맷팅 파이프라인 구축 — .editorconfig + CI/pre-push 강제 적용

### 완료

- [x] `.editorconfig` 작성 — 블록 스코프 네임스페이스 통일, 4-space indent, Allman 중괄호
- [x] `.gitattributes` 작성 — CRLF/LF 라인 엔딩 정규화
- [x] `PlatformA/Directory.Build.props` 작성 — `EnforceCodeStyleInBuild=true`
- [x] `dotnet format` 실행 — 파일 스코프 네임스페이스 12개 → 블록 스코프 변환, 전체 포맷 통일
- [x] `.github/workflows/ci.yml` — Format check 단계 추가 (`dotnet format --verify-no-changes`)
- [x] `.claude/hooks/pre-push-build-check.sh` — format check push 전 검증 추가

---

## 스프린트 #11 (2026-05-11 ~)
**목표**: 패킷 직렬화 Google Protocol Buffers 교체 (Generator.Lib 제거)

### 완료

- [x] 패킷 직렬화 Protobuf 교체 (proto 파일, Library csproj, PacketHandler, DummyClient, 테스트 재작성, Generator.Lib 삭제)

---

## 스프린트 #12 (2026-05-11 ~)
**목표**: TCP 프레이밍 Protobuf Envelope 전환 — packetId 헤더 제거 + DummyClient 단편화 수정

### 완료

- [x] Protobuf Envelope 전환 (packets.proto Packet oneof, PacketManager PayloadOneofCase, GameSession/PacketHandler/DummyClient 전체 교체)

---

## 스프린트 #13 (2026-05-11 ~)
**목표**: 대기열 처리 속도 동적 스케일링

### 완료

- [x] 대기열 처리 속도 동적 스케일링 (QUEUE_BASE_RATE/QUEUE_MAX_RATE 환경 변수, CalculateEffectiveRate 비례 알고리즘)

---

## 스프린트 #14 (2026-05-11 ~)
**목표**: 매칭 시스템 기능 개선 (취소, 타임아웃, 폴링 단축, 상태 API, Lua 원자 pop)

### 완료

- [x] `Consts.cs` — `MATCH_QUEUE_KEY`, `MATCH_TIMEOUT_SECONDS` 추가
- [x] `GameMatchService.cs` — Redis List → Sorted Set 전환, 타임아웃 정리, Lua 원자 2-player pop, 폴링 1000ms → 200ms
- [x] `GameMatchService.cs` — `RemovePlayerFromQueueAsync`, `GetQueueRankAsync`, `GetQueueLengthAsync` 추가
- [x] `GameMatchController.cs` — `DELETE /CancelMatch`, `GET /Status` 엔드포인트 추가, `ExtractPlayerId` 헬퍼 추출
- [x] `MatchingHub.cs` — 미사용 `EngineService` / `GameMatchService` 의존성 제거
- [x] `AI/adr/006-matching-improvement.md` 작성

---

## 스프린트 #15 (2026-05-12 ~)
**목표**: 시나리오 5 구현 — 1000명 매칭 시스템 부하 테스트

### 완료

- [x] `AuthHelper.cs` — `WaitUntilActiveAsync` 공통 헬퍼 추출 (SignalR + fallback 폴링 + 토큰 갱신)
- [x] `LoginWaitScenario_1.cs` — 추출된 헬퍼 호출로 교체 (중복 코드 제거)
- [x] `Scenarios/LoadTestMatchingScenario.cs` — 신규 생성 (매칭 등록, MatchFound 대기, P50/P95/P99 지연 통계)
- [x] `Program.cs` — case "5" 연결

---

## 스프린트 #16 (2026-05-12 ~)
**목표**: run-scenarios 스킬 작성 — 전체 DummyClient 시나리오 자동 실행

### 완료

- [x] `.claude/skills/run-scenarios/SKILL.md` — 신규 생성 (인프라 체크, 서버 자동 시작, stdin 파이핑, Redis 정리, 서버 종료, 콘솔 리포트)
- [x] `Program.cs` — 시나리오 1 메뉴 레이블 `[시나리오 1]` 접두사 통일

---

## 스프린트 #17 (2026-05-12 ~)
**목표**: docker-compose 파일 구조 정리 및 MariaDB·RabbitMQ compose 파일 추가

### 완료

- [x] `Redis/` → `PlatformA/docker/redis-cluster/` 디렉토리 이동
- [x] `PlatformA/docker/mariadb/docker-compose.yml` — 신규 생성 (실행 중 컨테이너 기반)
- [x] `PlatformA/docker/rabbitmq/docker-compose.yml` — 신규 생성 (실행 중 컨테이너 기반)
- [x] 세 compose 파일 `restart: always` 적용

---

## 스프린트 #18 (2026-05-12 ~)
**목표**: Ticketing.API 코드 정리 — 데모 코드 제거 및 보안 수정

### 완료

- [x] `Controllers/TicketController.cs` 삭제 (전체 데모/학습용 코드)
- [x] `Controllers/QueueController.cs` — 주석 블록 제거, `BadRequest(ex.Message)` → 일반 메시지, `StartsWith("Bearer")` → `StartsWith("Bearer ")`
- [x] `Hubs/QueueHub.cs` — base만 호출하는 빈 `OnDisconnectedAsync` 제거

---

## 스프린트 #19 (2026-05-13 ~)
**목표**: K8s 배포 기반 구성 — Consts.cs 환경변수화 및 HTTPS 전용 Dockerfile 정비

### 완료

- [x] `Consts.cs` — 8개 상수 `static readonly` + `Environment.GetEnvironmentVariable()` fallback 전환 (GAME_SERVER_IP, GAME_SERVER_PORT, REDIS_CONNECTION_STRING, AUTH_API_URL, AUTH_API_REFRESH_URL, TICKET_API_URL, MATCH_API_URL, MATCH_HUB_URL)
- [x] `DuplicateLoginScenario.cs` — 소켓 수명 버그 수정 (socket1/2를 RunAsync에서 소유, ConnectAndLoginAsync에 외부 소켓 주입)
- [x] `Auth.API/Dockerfile` — HTTPS 전용 포트 7001, HTTP 비활성화
- [x] `Matching.API/Dockerfile` — 신규 생성 (.NET 9.0, HTTPS 전용 포트 7002)
- [x] `Ticketing.API/Dockerfile` — HTTPS 전용 포트 7003, HTTP 비활성화
- [x] `Utils.API/Dockerfile` — HTTPS 전용 포트 7004, HTTP 비활성화
- [x] `Game.Server/Dockerfile` — EXPOSE 7777 추가
- [x] `docker/docker-compose.full.yml` — 신규 생성 (Redis 6-node cluster + MariaDB + 5개 서비스 전체 스택, TLS 인증서 볼륨 마운트)
- [x] `docker/certs/.gitignore` — *.pfx 제외
- [x] `.claude/skills/run-scenarios/SKILL.md` — 포트 업데이트 (7001/7002/7003/7004)

---

## 스프린트 #20 (2026-05-14 ~)
**목표**: DocFX 기반 공개 문서 사이트 구축 — GitHub Pages 자동 배포

### 완료

- [x] `Docs/docfx.json` — DocFX v2 설정 (metadata, build, custom 템플릿)
- [x] `Docs/toc.yml` + `Docs/index.md` — 홈 (아키텍처 다이어그램, 서비스 표, 기술 스택)
- [x] `Docs/templates/custom/partials/head.tmpl.partial` — Mermaid.js v10 CDN 주입
- [x] `Docs/architecture/overview.md` — C4 스타일 아키텍처 다이어그램 + 서비스 책임 표
- [x] `Docs/architecture/sequences.md` — 5개 시퀀스 다이어그램 (로그인·토큰 갱신·대기열·매칭·게임 세션)
- [x] `Docs/architecture/redis-keyspace.md` — Redis 키스페이스 맵 + 상세 명세표
- [x] `Docs/architecture/database-schema.md` — ER 다이어그램 + 상태 머신 + Migration 명령
- [x] `Docs/stakeholder/overview.md` — 놀이공원 비유 설명 + 핵심 수치 + 보안 특징
- [x] `Docs/stakeholder/user-journey.md` — 플레이어 여정 플로우차트 + Gantt 타임라인
- [x] `Docs/stakeholder/faq.md` — 비개발자 Q&A (용량·매칭·보안·데이터)
- [x] `.github/workflows/docs.yml` — GitHub Pages 자동 배포 (push to main → docfx → gh-pages)
- [x] `PlatformA.Library/PlatformA.Library.csproj` — `GenerateDocumentationFile` 추가
- [x] `PlatformA.Matching.API/PlatformA.Matching.API.csproj` — `GenerateDocumentationFile` 추가
- [x] `PlatformA.Ticketing.API/PlatformA.Ticketing.API.csproj` — `GenerateDocumentationFile` 추가
- [x] `PlatformA.Utils.API/PlatformA.Utils.API.csproj` — `GenerateDocumentationFile` 추가
- [x] `dotnet build PlatformA.sln` 오류 0개 확인 (CS1591 경고 277개는 정상)

---

## 스프린트 #21 (2026-05-18 ~)
**목표**: AI PipeLine 도입 초안작업

### 완료

- [x] `.claude/skills/qa-failure/SKILL.md` — /qa-failure 스킬 신규 추가 (CI 실패 자동 분석)
- [x] `PlatformA.Tests.Matching.API` — GameMatchController 통합 테스트 8케이스
- [x] `PlatformA.Tests.Ticketing.API` — QueueController 통합 테스트 8케이스
- [x] `AI/tasks/` — 경량 작업 상태 JSON 구조 + /plan & /done 스킬 연동
- [x] `AI/cost-log.md` — AI 작업 비용 추적 로그 신규 추가
- [x] 8개 SKILL.md — `schema_version: 1` 추가 (13개 전체 확인 완료)

---

## 스프린트 #22 (2026-05-21 ~)
**목표**: AI Pipeline 단기 개선 — /impact · /requirement 스킬 추가 및 상태 머신 6단계 전환

### 완료

- [x] `.claude/skills/impact/SKILL.md` — /impact 스킬 신규 추가 (IMPACT_ANALYSIS 단계)
- [x] `.claude/skills/requirement/SKILL.md` — /requirement 스킬 신규 추가 (REQUIREMENT_ANALYSIS 단계)
- [x] `AI/tasks/SCHEMA.md` — 상태 머신 4단계 → 6단계 (analyzing/coding/testing 추가)
- [x] `.claude/skills/plan/SKILL.md` — 초기 status `in_progress` → `analyzing` 변경
- [x] `.claude/skills/done/SKILL.md` — 상태 전환(coding→testing→done/failed) + 비용 자동 계산 추가
- [x] `PlatformA.Utils.API` — 코드 정리: 죽은 코드 제거, Redis 키 상수화(`Consts.cs`), SQLite 연결 문자열 외부화(`appsettings.json`)

---

## 스프린트 #23 (2026-05-21 ~)
**목표**: AI_SDLC 0~4단계 스킬 완성 — /start·/pr 신규 추가, /done 분리, /impact 버그 수정

### 완료

- [x] `.claude/skills/impact/SKILL.md` — `allowed-tools` 에 `Bash(ls *)` 추가 (spec 파일 읽기 버그 수정)
- [x] `.claude/skills/done/SKILL.md` — BUILD_GATE(커밋+빌드+테스트+push)만 담당하도록 슬림화
- [x] `.claude/skills/pr/SKILL.md` — 신규: PR_SUMMARY 전담 (SPRINT+PR+task JSON+cost-log)
- [x] `.claude/skills/start/SKILL.md` — 신규: CODE_FIX 진입점 (task coding 전환+작업 지시서)
- [x] `AI/AI_SDLC(pipeline).txt` — 스킬 워크플로 순서도 + 단계별 스킬 매핑 테이블 추가

---

## 스프린트 #24 (2026-05-22 ~)
**목표**: AI_SDLC Stage 5 연동 및 데드코드 정리 — /test-gen 신규, /done 정리, SCHEMA 갱신

### 완료

- [x] `.claude/skills/test-gen/SKILL.md` — /test-gen 스킬 신규 (Stage 5 TEST_CASE_GENERATION 연동)
- [x] `.claude/skills/done/SKILL.md` — 데드코드(6~9단계) 제거, description 수정
- [x] `AI/tasks/SCHEMA.md` — coding 담당 스킬 수정, test_generated·review_completed 필드 추가
- [x] `.claude/skills/plan/SKILL.md` — task JSON 템플릿에 신규 필드 추가
- [x] `.claude/skills/review/SKILL.md` — 완료 처리(review_completed 기록) 추가
- [x] `AI/AI_SDLC(pipeline).txt` — 워크플로·Phase2 갱신
- [x] `.claude/agents/test-writer.md` — Ticketing/Matching 테스트 현황 반영, 팩토리 패턴 추가 (FixTestWriterDoc)

---

## 스프린트 #25 (2026-05-22 ~)
**목표**: AI_SDLC Gate 강화 — impact/test/review 결과를 task JSON 기반으로 /pr 단계에서 검사

### 완료

- [x] `AI/tasks/SCHEMA.md` — impact·steps 필드 추가 및 예시 JSON 최신화
- [x] `.claude/skills/impact/SKILL.md` — impact 결과 task JSON 저장 (impact 필드 + steps 기록)
- [x] `.claude/skills/pr/SKILL.md` — test_generated·review_completed·impact 게이트 검사 추가
- [x] `.claude/skills/done/SKILL.md` — test-gen 미실행 경고 추가
- [x] `.claude/skills/plan/SKILL.md` — task JSON 템플릿에 impact·steps 필드 추가
- [x] `AI/AI_SDLC(pipeline).txt` — 단계 재번호 및 게이트 정책 반영

---

## 스프린트 #26 (2026-05-22 ~)
**목표**: PR 머지 자동 감지 — GitHub Actions로 task JSON·SPRINT.md·cost-log 자동 갱신

### 진행 중

- [x] `.github/workflows/pr-merge-sync.yml` — PR 머지 감지 워크플로우
- [x] `.github/scripts/sync_merged_pr.py` — task JSON + SPRINT.md + cost-log 자동 갱신

---

## 스프린트 #27 (2026-05-26 ~)
**목표**: AI_SDLC GitHub Actions Gate 강화 및 자동 리포트 기반 마련

### 완료

- [x] `.github/workflows/sdlc-gate-check.yml` — PR 단계 AI_SDLC gate check 추가
- [x] `.github/scripts/check_sdlc_gate.py` — test/review/impact 검사 구현
- [x] `.github/scripts/sync_merged_pr.py` — steps[] 기록, cost-log 중복 방지, summary 출력
- [x] `AI/tasks/SCHEMA.md` — GitHub Actions 연동 섹션 추가
- [x] `.github/scripts/generate_sdlc_report.py` — 주간 SDLC 리포트 생성 스크립트
- [x] `AI/AI_SDLC(pipeline).txt` — GitHub Actions 단계(10, 11) 반영


---

## 스프린트 #28 (2026-05-26 ~)
**목표**: cost-log 메트릭 강화 — duration_sec / consume_tokens 컬럼 추가

### 완료

- [x] `AI/cost-log.md` — duration_sec / consume_tokens / cache_tokens 컬럼 추가
- [x] `AI/tasks/SCHEMA.md` — 세 필드 정의 추가, 자동 계산 가이드
- [x] `.claude/skills/plan/SKILL.md` — task JSON 템플릿에 세 필드 추가
- [x] `.claude/skills/pr/SKILL.md` — count_tokens.py 호출로 자동 계산 및 기록
- [x] `.github/scripts/sync_merged_pr.py` — append_cost_log() cache_tokens 컬럼 포함
- [x] `.github/scripts/count_tokens.py` — JSONL 파싱 토큰 자동 계산 스크립트 신규

---

## 스프린트 #29 (2026-05-28 ~)
**목표**: 문서화 자동화 개선 — api-guide·DB 스키마 자동 생성 + MySqlDB.Lib·Game.Server DocFX 추가

### 완료

- [x] `.github/scripts/generate_api_docs.py` — 컨트롤러 XML 주석·DTO·오류 패턴 파싱 → `Docs/api-guide/*.md` 4개 자동 갱신
- [x] `.github/scripts/generate_db_schema.py` — Entity 클래스 파싱 → `Docs/architecture/database-schema.md` 테이블 명세 섹션 교체
- [x] `.github/workflows/docs.yml` — 두 스크립트 스텝 추가, MySqlDB.Lib·Game.Server 경로 트리거 확장
- [x] `Docs/docfx.json` — MySqlDB.Lib·Game.Server csproj 메타데이터 추가 (111개 API 파일)
- [x] `PlatformA.MySqlDB.Lib`, `PlatformA.Game.Server` — `GenerateDocumentationFile=true`, `CS1591` 억제
- [x] `PlatformA.Game.Server` — GameRoom·GameRoomManager·GameSession·PacketHandler XML 주석 추가
- [x] `Docs/developer-guide/game-server-architecture.md` — TCP 구조·JobQueue·분산락 설계 문서 신규

---

## 스프린트 #30 (2026-05-29 ~)
**목표**: .NET 8/9 → 10 전체 TFM 통일 — 빌드 실패 해소 및 SDK 통일

### 완료

- [x] `global.json` — SDK `9.0.100` → `10.0.300` (빌드 실패 즉시 해소)
- [x] 13개 `.csproj` — TargetFramework `net8.0`/`net9.0` → `net10.0` 전체 통일
- [x] `Pomelo.EntityFrameworkCore.MySql` 8.0.2 → 9.0.0 (10.x 미출시)
- [x] `EFCore.NamingConventions` 8.0.3 → 9.0.0
- [x] `Microsoft.EntityFrameworkCore.*` 8.0.x → 9.0.16 (Pomelo 제약으로 9.x 유지)
- [x] `AspNetCore.HealthChecks.Redis` 8.0.1 → 9.0.0
- [x] `Microsoft.AspNetCore.Mvc.Testing` → 10.0.8
- [x] 6개 `Dockerfile` — base image `8.0`/`9.0` → `10.0`
- [x] `.github/workflows/ci.yml` — `dotnet-version: 9.0.x` → `10.0.x`
- [x] .NET 10 breaking change 3건 수정 (RedisValue 오버로드 모호성 캐스팅, AuthTestWebAppFactory EF Core 9 다중 provider 검증 대응)

### 진행 중

- [x] **빌드 수정**: session-start 훅 `-q` → `--verbosity minimal`, Game.Server 누락 패키지 추가
- [x] **OpenAPI 현대화**: Swashbuckle 6.6.2 제거 → `Microsoft.AspNetCore.OpenApi 10.0.8` + `Scalar.AspNetCore 2.14.14` 도입 (Auth/Ticketing/Matching API)
- [x] **BearerSecuritySchemeTransformer**: 각 API 프로젝트에 신규 파일 추가, JWT 보안 스키마 등록 (OpenApi 2.0 네임스페이스 대응)
- [x] **C# 현대화**: `Directory.Build.props`에 `LangVersion=latest` 추가, Service/Controller primary constructors 6개, collection expression 적용
- [x] **검증**: `dotnet build` 오류 0 + `dotnet test` 113/113 통과

---

## 스프린트 #31 (2026-05-30 ~)
**목표**: 문서 메타데이터 자동화 — .NET 버전·테스트 수를 코드에서 동적으로 읽어 문서 최신화

### 진행 중

- [x] `generate_api_docs.py`: SERVICES 리스트의 하드코딩 `.NET 8.0/9.0` → csproj XML 파싱으로 동적 추출
- [x] `generate_doc_meta.py` 신규: 테스트 수(`[Fact]`/`[Theory]` 카운팅) + .NET 버전 → `Docs/index.md` 갱신
- [x] `Docs/architecture/overview.md`: 런타임 버전 테이블 마커 기반 자동 갱신 (.NET 10.0)
- [x] `docs.yml`: `generate_doc_meta.py` 실행 스텝 추가
- [x] 검증: `Docs/index.md`에 `.NET 10.0`, `111개 테스트` 반영 확인

---

## 스프린트 #32 (2026-06-02 ~)
**목표**: 문서화 자동화 확장 — proto·Redis 키 변경 시 패킷 프로토콜·키스페이스 문서 자동 갱신

### 진행 중

- [x] `generate_proto_docs.py` 신규: packets.proto 메시지·필드 파싱 → `Docs/developer-guide/packet-protocol.md` 마커 구간 자동 갱신
- [x] `generate_redis_key_docs.py` 신규: Consts.cs Redis 키 상수 파싱 → `Docs/architecture/redis-keyspace.md` 마커 구간 자동 갱신
- [x] `packet-protocol.md`, `redis-keyspace.md` 마커 삽입 (수동 1회)
- [x] `docs.yml` 두 스크립트 실행 스텝 추가
- [x] 검증: 스크립트 로컬 실행 후 문서 내용 정확성 확인

---

## 스프린트 #33 (2026-06-02 ~)
**목표**: `System.Threading.Lock` 전환 — object _lock 3곳을 .NET 10 전용 Lock 타입으로 교체

### 진행 중

- [x] `JobQueue.cs`: `private object _lock` → `private readonly Lock _lock`
- [x] `SnowflakeGenerator.cs`: `private static readonly object _lock` → `private static readonly Lock _lock`
- [x] `SessionManager.cs`: `private readonly object _lock` → `private readonly Lock _lock`
- [x] `/test-gen`: JobQueue·SessionManager 스레드 안전성 테스트 케이스 생성
- [x] 검증: `dotnet build` 오류 0 + `dotnet test` 전체 통과

---

## 스프린트 #34 (2026-06-02 ~)
**목표**: `/pr` cost-log 기록 안정화 — Windows 환경에서 duration_sec·consume_tokens·cache_tokens가 간헐적으로 누락되는 문제 수정

### 진행 중

- [x] `count_tokens.py`에 duration_sec 계산 통합 (date -d 제거)
- [x] `python` → `python3` 폴백 처리 또는 단일 스크립트 호출로 통합
- [x] `/pr` 스킬 4단계에서 date -d 의존 코드 제거, Python 단일 호출로 교체
- [x] 검증: 스크립트 로컬 실행 후 duration·tokens 값 정상 출력 확인

---

## 스프린트 #35 (2026-06-03 ~)
**목표**: 외부 사용자 Docker 원클릭 환경 구성 — setup 스크립트 1회 실행 후 `docker compose up`으로 전체 스택 동작

### 진행 중

- [x] `docker/mariadb/init/01-create-databases.sql` — MariaDB 최초 기동 시 db_WebApp·db_LogApp 자동 생성
- [x] `PlatformA.MySqlDB.Lib/Dockerfile.migrator` — EF Core 마이그레이션 자동화 컨테이너
- [x] `docker-compose.full.yml` — db-migrator 서비스 추가, initdb.d 마운트, .env 변수화, SQLite 볼륨, DummyClient profile
- [x] `docker/setup.sh` + `docker/setup.ps1` — 인증서 생성 및 .env 초기화 자동화
- [x] `docker/.env.example` — 환경변수 템플릿
- [x] 검증: `docker compose up` 후 모든 서비스 healthcheck 통과 확인

---

## 스프린트 #36 (2026-06-03 ~)
**목표**: Phase 3 AI_SDLC 인프라 Docker 설치 — PostgreSQL(ai_sdlc 전용) + n8n 로컬 구동

### 진행 중

- [x] `docker/sdlc/docker-compose.yml` — PostgreSQL 16 + n8n 서비스 정의
- [x] `docker/sdlc/.env.example` — SDLC 인프라 환경변수 템플릿
- [x] `docker/sdlc/setup-sdlc.sh` + `setup-sdlc.ps1` — 원터치 SDLC 스택 시작 스크립트
- [x] 검증: `docker compose up` 후 PostgreSQL 연결 및 n8n UI 접근 확인

---

## 스프린트 #37 (2026-06-03 ~)
**목표**: `/pr` requirement 차단 강화 + sprint35·36 requirement 소급 기록

### 진행 중

- [x] `.claude/skills/pr/SKILL.md` 검사 6 — CODE_CHANGED 무관하게 requirement 미실행 시 차단(❌)으로 강화
- [x] `.claude/plan/2026-06-03_002_SetupDockerOneClick.md` — sprint35 요구사항 명세 소급 생성
- [x] `AI/tasks/sprint35_SetupDockerOneClick.json` — requirement 단계 소급 추가
- [x] sprint36(AddSdlcDockerInfra) requirement 소급: `.claude/plan/2026-06-03_001_AddSdlcDockerInfra.md`

---

## 스프린트 #38 (2026-06-03 ~)
**목표**: `/pr` 4.5단계 명세 파일 archived 날짜 의존성 제거

### 진행 중

- [x] `.claude/skills/pr/SKILL.md` 4.5단계 — `${TODAY}_*_${PLAN_NAME}` → `*_${PLAN_NAME}` 패턴으로 교체
- [x] 검증: 날짜가 달라도 명세 파일이 `processed/`로 이동되는지 확인

---

## 스프린트 #39 (2026-06-03 ~)
**목표**: n8n·PostgreSQL Docker 구조 재편 — sdlc 통합 폴더를 독립 폴더로 분리하고 full compose에 통합

### 진행 중

- [x] `docker/postgresql/docker-compose.yml` 생성 — 독립 실행용 PostgreSQL 16 (기본값 내장)
- [x] `docker/n8n/docker-compose.yml` 생성 — 독립 실행용 n8n (SQLite 백엔드, postgres 의존 없음)
- [x] `docker/docker-compose.full.yml` — postgres·n8n 서비스 및 볼륨 추가 (platformA-net 공유)
- [x] `docker/.env.example` — PostgreSQL/n8n 환경변수 섹션 추가
- [x] `docker/sdlc/` 폴더 및 스크립트(.env, .env.example, setup-sdlc.ps1, setup-sdlc.sh) 제거
- [x] 로컬 Docker 독립 실행 테스트 통과 (postgresql, n8n 각각)

---

## 스프린트 #40 (2026-06-03 ~)
**목표**: ADR-008·009 소급 생성 + /requirement DESIGN_REVIEW 워크플로 개선

### 진행 중

- [x] `AI/adr/008-n8n-event-orchestrator.md` — n8n 이벤트 오케스트레이터 채택 ADR 생성
- [x] `AI/adr/009-postgresql-sdlc-db.md` — PostgreSQL SDLC 전용 DB 채택 ADR 생성
- [x] `.claude/skills/requirement/SKILL.md` — DESIGN_REVIEW 5단계에 기술 도입 체크리스트 추가

---

## 스프린트 #41 (2026-06-04 ~)
**목표**: AI_SDLC Phase 3 PostgreSQL 상태 저장소 MVP — `PlatformA.SdlcDB.Lib` EF Core 기반 구축

### 진행 중

- [x] `PlatformA.SdlcDB.Lib` 프로젝트 생성 + sln 등록 + csproj (Npgsql 9.x)
- [x] `AiJob` / `AiJobStep` / `AiFailure` / `AiModelRun` Entity 생성
- [x] `SdlcDbContext` + IDesignTimeDbContextFactory (sdlc 스키마 분리)
- [x] `InitialSdlcDb` EF Core Migration 생성
- [x] `Dockerfile.migrator` 생성 (CMD 패턴, dotnet-ef 9.*)
- [x] `docker-compose.full.yml` + `docker/postgresql/docker-compose.yml` — sdlc-db-migrator 추가
- [x] `.github/scripts/migrate_tasks_to_postgres.py --dry-run` 구현
- [x] 검증: dotnet build + ef database update + dry-run 실행 확인

---

## 스프린트 #42 (2026-06-04 ~)
**목표**: CI 실패 자동 감지·기록·수정 파이프라인 — n8n + PostgreSQL 기반 실패 추적 구축

### 진행 중

- [x] `/done` 스킬 — `dotnet format --verify-no-changes` 검증 + 실패 시 자동 수정·재커밋
- [x] `.github/workflows/auto-format.yml` — CI format 실패 시 자동 fix 커밋·재실행
- [x] `.github/scripts/record_failure.py` — ai_failures PostgreSQL INSERT 헬퍼
- [x] `.github/scripts/check_sdlc_gate.py` 수정 — gate 실패 시 record_failure 기록
- [x] `.n8n/workflows/github-failure-monitor.json` — GitHub API 폴링 → 실패 감지 → PostgreSQL INSERT
- [x] `.claude/hooks/session-start.sh` 수정 — ai_failures 미해결 건 조회·표시

---

## 스프린트 #43 (2026-06-05 ~)
**목표**: AI_SDLC Phase 3 데이터 흐름 안정화 — ai_failures 중복 방지, task JSON DB 이전, n8n INSERT 보완

### 진행 중

- [x] `AiFailure` Entity: `GitHubRunId`/`GitHubJobId`/`CommitSha`/`Branch` 컬럼 추가, `Metadata` `text` → `jsonb`
- [x] EF Core Migration: `AddGitHubFailureIdentity` + partial unique index `(github_run_id, github_job_id, failure_type) WHERE NOT NULL`
- [x] `migrate_tasks_to_postgres.py --apply` 구현 — ai_jobs/ai_job_steps upsert (psycopg2)
- [x] `record_failure.py`: `--run-id`/`--job-id`/`--commit-sha` 인수 추가, ON CONFLICT 지원
- [x] n8n `github-failure-monitor.json`: INSERT에 `github_run_id`/`github_job_id` 직접 컬럼 추가
- [x] `Docs/operations/ai-sdlc-n8n-failure-monitor.md` 신규 작성

---

## 스프린트 #44 (2026-06-05 ~)
**목표**: 워크플로 완전 자동화 기반 구축 — 스킬 차단 요인 제거 + /workflow 오케스트레이터 신규 생성

### 진행 중

- [x] `/done` · `/pr` — `disable-model-invocation: true` → `false` (Claude Skill 도구 호출 허용)
- [x] `/review` — `allowed-tools` 선언 추가 + `steps[]` 기록 로직 추가
- [x] `/start` · `/test-gen` — `steps[]` 기록 로직 추가
- [x] `/workflow` 오케스트레이터 스킬 신규 생성 (plan → pr 전체 체인)
- [x] `CLAUDE.md` · `AI/AI_SDLC(pipeline).txt` — 자동화 모드 문서화

---

## 스프린트 #45 (2026-06-05 ~)
**목표**: Phase 3 자동화 완성 — /done steps[] 보완, 계획 파일 Push 자동 트리거, CI 실패 자동 수정 루프, cost-log 역산

### 진행 중

- [x] `.claude/skills/done/SKILL.md` — 4.5단계에 steps[] 기록 추가 (`"name": "done"` 항목)
- [x] `.claude/skills/pr/SKILL.md` — python3 → python 우선 순서 변경 (Windows Store stub 회피)
- [x] `.github/scripts/count_tokens.py` — 디버그 모드 환경변수 추가
- [x] `.github/scripts/backfill_cost_log.py` — Sprint #39-#44 누락 토큰 역산 스크립트 신규 생성
- [x] `.github/workflows/plan-file-trigger.yml` — 계획 파일 Push → /workflow 자동 실행
- [x] `.github/workflows/auto-fix.yml` — repository_dispatch → /qa-failure 자동 수정
- [x] `.n8n/workflows/github-failure-monitor.json` — fixable_by_ai 필터 + dispatch 노드 추가
- [x] `AI/AI_SDLC(pipeline).txt` — Phase3 완성도 ~90% 업데이트

---

## 스프린트 #46 (2026-06-05 ~)
**목표**: ai_model_runs 연동 — /pr 완료 시 토큰 사용량을 PostgreSQL에 자동 기록

### 진행 중

- [ ] `.github/scripts/insert_model_run.py` — 신규 작성: count_tokens.py 결과 + task JSON → sdlc.ai_model_runs INSERT (psycopg2, 연결 실패 시 경고 후 계속)
- [ ] `.claude/skills/pr/SKILL.md` — 4단계(cost-log 기록) 완료 후 insert_model_run.py 호출 추가
- [ ] 로컬 PostgreSQL 환경에서 실제 INSERT 검증 (ai_model_runs 행 확인)
- [ ] cost-log.md 병행 유지 확인 (DB 연결 실패 시에도 cost-log.md는 정상 기록)
