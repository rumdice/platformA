# CLAUDE.md — AI 운영 지침서

이 파일은 Claude Code가 세션 시작 시 가장 먼저 읽는 파일입니다.
모든 행동의 기준이 됩니다. 모호한 상황에서는 이 파일을 우선합니다.

---

## 프로젝트 개요

**PlatformA** — .NET 기반 고성능 분산 멀티플레이어 게임 백엔드 플랫폼

- **언어/런타임**: C# / .NET 8.0 (Matching.API만 .NET 9.0)
- **솔루션 파일**: `PlatformA/PlatformA.sln`
- **개발 브랜치**: `claude/analyze-project-structure-oWGle`
- **원격 저장소**: `rumdice/platforma`

---

## 세션 시작 시 필수 절차

1. `docs/SPRINT.md` 확인 → 현재 진행 중인 태스크 파악
2. 관련 `docs/adr/` 확인 → 이미 결정된 사항은 재논의하지 않음
3. 아래 빌드 명령으로 현재 상태 확인:

```bash
cd /home/user/platformA/PlatformA && dotnet build PlatformA.sln
```

---

## 핵심 명령어

```bash
# 빌드
cd /home/user/platformA/PlatformA && dotnet build PlatformA.sln

# 특정 프로젝트만 빌드
dotnet build PlatformA/PlatformA.Auth.API/PlatformA.Auth.API.csproj

# Redis 클러스터 시작 (Linux)
cd /home/user/platformA/Redis && docker-compose up -d

# DB Migration 생성 (WebApp)
cd /home/user/platformA/PlatformA/PlatformA.MySqlDB.Lib
dotnet ef migrations add <이름> --context DbWebAppContext --output-dir Migrations/WebApp

# DB Migration 적용 (WebApp)
dotnet ef database update --context DbWebAppContext

# Docker 이미지 빌드 (Auth API 예시)
cd /home/user/platformA/PlatformA
docker build -f PlatformA.Auth.API/Dockerfile -t platformA-auth:latest .
```

---

## 코드 변경 규칙

### 패킷 추가
- 반드시 `docs/PATTERNS.md`의 "패킷 추가 패턴" 따를 것
- `PlatformA.Generator.Lib` 통해 코드 생성 (수동 직렬화 금지)

### API 엔드포인트 추가
- `docs/API_CONTRACTS.md` 먼저 업데이트 → 그 다음 구현
- 컨트롤러 패턴은 `docs/PATTERNS.md` 참조

### DB 스키마 변경
- **반드시** EF Core Migration 생성 후 적용
- 직접 SQL 실행 금지
- Migration 없이 스키마 변경 절대 금지

### Redis 키 추가
- `PlatformA.Library/Common/Consts.cs`에 키 상수 추가
- 하드코딩된 문자열 키 사용 금지

### 설정값 변경
- `PlatformA.Library/Common/Consts.cs`에서만 변경
- `appsettings.json`은 로그 레벨만 관리

---

## 작업 완료 기준 (Definition of Done)

모든 태스크는 아래 조건을 충족해야 완료로 간주:

- [ ] `dotnet build PlatformA.sln` 빌드 오류 없음
- [ ] 해당 기능을 DummyClient 시나리오로 검증 가능한 경우 검증
- [ ] `docs/SPRINT.md` 해당 항목 체크
- [ ] 관련 API 변경 시 `docs/API_CONTRACTS.md` 업데이트
- [ ] git commit + push (브랜치: `claude/analyze-project-structure-oWGle`)

---

## 의사결정 규칙

| 상황 | 행동 |
|------|------|
| 설계 방향 판단 필요 | **사용자에게 질문** — 임의 구현 금지 |
| 기존 ADR과 충돌하는 요구 | ADR 내용 설명 후 사용자 승인 요청 |
| 환경 문제 발생 | `docs/ENVIRONMENT.md` 참조 |
| 패턴 불명확 | `docs/PATTERNS.md` 참조 |
| 보안 관련 변경 | 반드시 사용자 확인 후 진행 |
| DB 데이터 삭제/초기화 | **반드시** 사용자 확인 후 진행 |

---

## 절대 하지 말 것

- `main` 브랜치에 직접 push
- Migration 없이 DB 스키마 변경
- `Consts.cs` 외 위치에 설정값 하드코딩
- 테스트/검증 없이 배포 명령 실행
- 이미 ADR로 결정된 사항을 사용자 승인 없이 변경
- `--no-verify` 플래그로 git hook 우회

---

## 문서 참조 가이드

| 질문 | 참조 문서 |
|------|---------|
| 이 시스템은 어떻게 설계되었나? | `docs/ARCHITECTURE.md` |
| 왜 이런 기술을 선택했나? | `docs/adr/` |
| 지금 뭘 해야 하나? | `docs/SPRINT.md` |
| 앞으로 뭘 해야 하나? | `docs/BACKLOG.md` |
| 어떻게 빌드/배포하나? | `docs/RUNBOOK.md` |
| 로컬 환경 어떻게 세팅하나? | `docs/ENVIRONMENT.md` |
| API 스펙은 어떻게 되나? | `docs/API_CONTRACTS.md` |
| 코드는 어떤 패턴으로 작성하나? | `docs/PATTERNS.md` |
| 게임/비즈니스 규칙은? | `docs/DOMAIN.md` |
| 테스트는 어떻게 하나? | `docs/TESTING_STRATEGY.md` |
