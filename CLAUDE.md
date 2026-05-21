# CLAUDE.md — AI 운영 지침서

이 파일은 Claude Code가 세션 시작 시 가장 먼저 읽는 파일입니다.
모든 행동의 기준이 됩니다. 모호한 상황에서는 이 파일을 우선합니다.

---

## 프로젝트 개요

**PlatformA** — .NET 기반 고성능 분산 멀티플레이어 게임 백엔드 플랫폼

- **언어/런타임**: C# / .NET 8.0 (Matching.API만 .NET 9.0)
- **솔루션 파일**: `PlatformA/PlatformA.sln`
- **원격 저장소**: `rumdice/platforma`

---

## Git 워크플로 (로컬/웹 공통)

**모든 환경에서 동일한 브랜치 기반 워크플로를 사용한다.**

### 브랜치 네이밍 규칙
```
YYYY-MM-DD_PlanName

예) 2026-05-12_AddAuthTests
    2026-05-12_FixRedisBug
```
- `YYYY-MM-DD`: `/plan` 실행 시점의 **실제 오늘 날짜** (`date +%Y-%m-%d` 결과)
- `PlanName`: 사용자 설명에서 Claude가 PascalCase로 자동 생성 (최대 30자)
- 카운터 접미사(`_N`) 없음 — 같은 날 여러 작업은 PlanName으로 구분

### 표준 워크플로
```
/plan 작업 설명    → main 이동 + pull → 브랜치 생성 → SPRINT.md 업데이트
  (작업 수행)
/done              → 빌드 → 포맷 → 테스트 → push → 한글 PR 생성 → SPRINT 완료 체크
  (사용자가 GitHub에서 PR 머지)
```

> **주의**: Plan mode(설계 승인)와 `/plan` 스킬(브랜치 생성)은 별개다.
> Plan mode에서 승인 후 구현을 시작하기 전에 반드시 `/plan` 스킬을 실행하거나
> 수동으로 작업 브랜치를 생성해야 한다.

### /plan 브랜치 결정 규칙

| 현재 브랜치 PR 상태 | /plan 동작 |
|---|---|
| PR `OPEN` (미머지) | 해당 브랜치에서 계속 작업 — main 이동 없음 |
| 그 외 모든 경우 (main, PR MERGED, PR 없음) | `git checkout main && git pull` 후 새 브랜치 생성 |

---

## 세션 시작 시 필수 절차

`session-start.sh` 훅이 세션 시작 시 자동으로 **SPRINT.md 요약**과 **빌드 상태**를 제공한다.
훅 출력을 참조하고, 아래 경우에만 추가 조사한다:

1. 작업 관련 `AI/adr/` 확인 — 이미 결정된 사항은 재논의하지 않음
2. 훅 출력에 빌드 실패가 있으면 → 원인 파악 후 수정

---

## 핵심 명령어

```bash
# 빌드 (프로젝트 루트 기준)
cd PlatformA && dotnet build PlatformA.sln

# 특정 프로젝트만 빌드
dotnet build PlatformA/PlatformA.Auth.API/PlatformA.Auth.API.csproj

# 빌드 캐시 오류(MSB3492) 해결
cd PlatformA && dotnet clean PlatformA.sln && dotnet build PlatformA.sln

# DB Migration 생성 (WebApp)
cd PlatformA/PlatformA.MySqlDB.Lib
dotnet ef migrations add <이름> --context DbWebAppContext --output-dir Migrations/WebApp

# DB Migration 적용 (WebApp)
dotnet ef database update --context DbWebAppContext

# Docker 이미지 빌드 (Auth API 예시)
cd PlatformA
docker build -f PlatformA.Auth.API/Dockerfile -t platformA-auth:latest .
```

### 로컬 실행 순서 (의존성 순서 준수)

```bash
# 1. Redis 클러스터 시작
cd PlatformA/docker/redis-cluster && docker-compose up -d

# 2. MySQL DB 초기화 (최초 1회)
mysql -u root -ppass1234 -e "CREATE DATABASE IF NOT EXISTS db_WebApp; CREATE DATABASE IF NOT EXISTS db_LogApp;"

# 3. Migration 적용
cd PlatformA/PlatformA.MySqlDB.Lib
dotnet ef database update --context DbWebAppContext
dotnet ef database update --context DbLogAppContext

# 4. 서비스 실행 (각 터미널)
cd PlatformA/PlatformA.Auth.API    && dotnet run   # :7001
cd PlatformA/PlatformA.Ticketing.API && dotnet run  # :7003
cd PlatformA/PlatformA.Matching.API  && dotnet run  # :7002
cd PlatformA/PlatformA.Game.Server   && dotnet run  # :7777
```

---

## 코드 변경 규칙

> 도메인별 상세 규칙은 `.claude/rules/` 참조 (C# 파일 작업 시 자동 로드됨)
> - 코딩 패턴 전체 (패킷·API·Redis·EF Core·DTO·서비스·헬스체크): `.claude/rules/patterns.md`
> - 테스트: `.claude/rules/tests.md`

### Redis 키 추가
- `PlatformA.Library/Common/Consts.cs`에 키 상수 추가
- 하드코딩된 문자열 키 사용 금지

### 설정값 변경
- `PlatformA.Library/Common/Consts.cs`에서만 변경
- `appsettings.json`은 로그 레벨만 관리

---

## 작업 완료 기준 (Definition of Done)

모든 태스크는 아래 조건을 충족해야 완료로 간주:

- [ ] `dotnet build PlatformA.sln` 빌드 오류 없음 ← **push 전 반드시 통과**
- [ ] `dotnet test` 전체 통과 ← **빌드 통과 후 실행**
- [ ] 해당 기능을 DummyClient 시나리오로 검증 가능한 경우 검증
- [ ] `AI/SPRINT.md` 해당 항목 체크
- [ ] 관련 API 변경 시 `/doc-writer api-guide` 실행으로 `Docs/api-guide/` 동기화
- [ ] `/done` 실행 → PR 생성 완료

## Push 전 필수 빌드 검증 절차

**코드 변경이 포함된 모든 커밋은 push 전 아래 순서를 반드시 실행한다:**

```bash
# 1. 전체 솔루션 빌드 — 오류 0개 확인
cd PlatformA && dotnet build PlatformA.sln

# 2. 전체 테스트 실행 — 실패 0개 확인
dotnet test PlatformA.sln

# 3. 둘 다 통과한 경우에만 push → /done 이 자동으로 수행
```

> 빌드 또는 테스트 실패 시 push 금지. 오류를 수정한 뒤 재실행.

---

## 의사결정 규칙

| 상황 | 행동 |
|------|------|
| 설계 방향 판단 필요 | **사용자에게 질문** — 임의 구현 금지 |
| 기존 ADR과 충돌하는 요구 | ADR 내용 설명 후 사용자 승인 요청 |
| 환경 문제 발생 | 이 파일 **로컬 실행 순서** 참조 |
| 패턴 불명확 | `.claude/rules/patterns.md` 자동 로드됨 |
| 보안 관련 변경 | 반드시 사용자 확인 후 진행 |
| DB 데이터 삭제/초기화 | **반드시** 사용자 확인 후 진행 |

---

## 절대 하지 말 것

- `main` 브랜치에 직접 push ← **로컬/웹 모두 금지. 반드시 /plan → /done 워크플로 사용**
- **작업 브랜치 없이 구현 시작** ← Plan mode 승인 후에도 브랜치가 없으면 먼저 생성
- Migration 없이 DB 스키마 변경
- `Consts.cs` 외 위치에 설정값 하드코딩
- 테스트/검증 없이 배포 명령 실행
- 이미 ADR로 결정된 사항을 사용자 승인 없이 변경
- `--no-verify` 플래그로 git hook 우회
- **빌드(`dotnet build`), 포맷(`dotnet format`), 테스트(`dotnet test`) 실패 상태로 push**
- **SPRINT.md에 신규 스프린트를 기존 항목 위에 삽입** ← 항상 파일 맨 끝에 추가

---

## 문서 참조 가이드

| 질문 | 참조 문서 |
|------|---------|
| 이 시스템은 어떻게 설계되었나? | `AI/ARCHITECTURE.md` |
| 왜 이런 기술을 선택했나? | `AI/adr/` |
| 지금 뭘 해야 하나? | `AI/SPRINT.md` |
| 어떻게 빌드/실행하나? | 이 파일 **핵심 명령어 / 로컬 실행 순서** 섹션 |
| API 스펙은 어떻게 되나? | `Docs/api-guide/` (소스에서 자동 생성, `/doc-writer api-guide` 로 동기화) |
| 코드는 어떤 패턴으로 작성하나? | `.claude/rules/patterns.md` (C# 파일 작업 시 자동 로드) |
| 테스트는 어떻게 하나? | `test-writer` 에이전트 + `/run-scenarios` 스킬 |
