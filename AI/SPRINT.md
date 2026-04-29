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
