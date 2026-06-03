# 요구사항 명세: AddSdlcDbLib

작성일: 2026-06-04
브랜치: 2026-06-04_AddSdlcDbLib
소스: plan mode (~/.claude/plans/n8n-docker-cheeky-allen.md)

## 요구사항 요약

ADR-008(n8n)·ADR-009(PostgreSQL)로 인프라가 준비됐지만 실제 SDLC 상태를 저장하는 EF Core 라이브러리가 없다.
기존 `PlatformA.MySqlDB.Lib`의 EF Core 패턴(snake_case, IDesignTimeDbContextFactory, Dockerfile.migrator, CMD 패턴)을 그대로 재활용하여
`PlatformA.SdlcDB.Lib` 신규 프로젝트를 구축한다.
n8n이 같은 DB의 `n8n` 스키마를 사용하므로 SdlcDB는 `sdlc` 스키마로 분리한다.

## 상세 요구사항

1. **`PlatformA.SdlcDB.Lib` 신규 프로젝트 생성**
   - `dotnet new classlib -n PlatformA.SdlcDB.Lib`
   - `PlatformA.sln`에 추가
   - TargetFramework: `net10.0`, Nullable/ImplicitUsings enabled
   - 패키지: `Npgsql.EntityFrameworkCore.PostgreSQL 9.0.*`, `EFCore.NamingConventions 9.0.0`, `Microsoft.EntityFrameworkCore.Design 9.0.16`

2. **Entity 4개 생성** (`Entities/` 폴더)
   - `AiJob`: task JSON 1건 = 1행. branch unique. `source_json jsonb`, `impact jsonb`, `inserted_at`, `updated_at`
   - `AiJobStep`: steps[] 항목. FK → AiJob (Cascade). `result_json jsonb`
   - `AiFailure`: 빌드/테스트/gate 실패 기록. FK → AiJob (nullable)
   - `AiModelRun`: 토큰 사용량·비용 추정. FK → AiJob (nullable)

3. **`SdlcDbContext` 생성**
   - `HasDefaultSchema("sdlc")` — n8n 스키마와 분리
   - `UseNpgsql().UseSnakeCaseNamingConvention()`
   - 주요 인덱스: `ai_jobs.branch` unique, `ai_jobs.status`, `ai_job_steps.job_id`, `ai_failures.job_id`
   - FK cascade 설정
   - `IDesignTimeDbContextFactory<SdlcDbContext>` — Context 파일 내 중첩 구현, `SDLC_DB_CONNECTION` 환경변수 사용

4. **`InitialSdlcDb` EF Core Migration 생성**
   - `dotnet ef migrations add InitialSdlcDb --context SdlcDbContext`

5. **`Dockerfile.migrator` 생성** (CMD 패턴, 기존 MySqlDB.Lib 방식 동일)
   - SDK 10.0 이미지, `dotnet-ef 9.*` global install
   - `CMD dotnet ef database update --context SdlcDbContext`
   - 환경변수 `SDLC_DB_CONNECTION`

6. **docker-compose 수정** — `sdlc-db-migrator` 서비스 추가
   - `docker/docker-compose.full.yml`: postgres healthy 후 실행
   - `docker/postgresql/docker-compose.yml`: 독립 실행 지원

7. **`migrate_tasks_to_postgres.py --dry-run` 구현**
   - `AI/tasks/*.json` 전체 파싱
   - `ai_jobs`·`ai_job_steps` 매핑 가능 여부 검증
   - DB 연결 없이 동작 (dry-run 기본)
   - 출력: job count, step count, broken JSON 목록, status별 집계

## 영향 범위 (예상)

| 파일/폴더 | 변경 유형 |
|-----------|----------|
| `PlatformA.SdlcDB.Lib/` | 신규 프로젝트 전체 |
| `PlatformA.sln` | 수정 (프로젝트 추가) |
| `docker/docker-compose.full.yml` | 수정 (서비스 추가) |
| `docker/postgresql/docker-compose.yml` | 수정 (서비스 추가) |
| `.github/scripts/migrate_tasks_to_postgres.py` | 신규 |

기존 게임 서비스(Auth/Matching/Ticketing/Utils/Game) C# 코드 변경 없음.

## 제약 및 주의사항

- **ADR-009**: PostgreSQL 사용 — 동일 `platforma_sdlc` DB, `sdlc` 스키마로 n8n과 분리
- **기존 MySqlDB.Lib 건드리지 않음**: 별도 프로젝트로 완전 분리
- **MySQL 전용 설정 사용 금지**: `UseMySql`, `ServerVersion.AutoDetect`, `UseCollation("utf8mb4_...")` 금지
- **dotnet-ef 버전**: `9.*` 고정 (EF Core 패키지 9.x 기준)
- **jsonb 초기 매핑**: `string?`으로 선언 후 `HasColumnType("jsonb")` — 추후 JsonDocument로 전환 가능
- **Phase 3 초기**: PostgreSQL은 mirror, `AI/tasks/*.json`이 primary source of truth

## 구현 접근 방향

1. `dotnet new classlib` → csproj 수동 편집 (패키지 추가)
2. Entities/ 폴더 아래 4개 Entity 작성
3. `SdlcDbContext.cs`에 DbSet + OnModelCreating + 중첩 Factory 구현
4. `dotnet ef migrations add` 실행
5. `Dockerfile.migrator` 작성 (MySqlDB.Lib 방식 그대로)
6. docker-compose 2개 파일 수정
7. Python dry-run 스크립트 작성
8. `dotnet build` → `dotnet ef database update` (postgres 컨테이너 필요) → dry-run

## 검증 기준

- [ ] `dotnet build PlatformA.sln` — 오류 0개
- [ ] `PlatformA.SdlcDB.Lib/Migrations/` 폴더에 InitialSdlcDb 파일 존재
- [ ] `dotnet ef database update --context SdlcDbContext` 성공
- [ ] `SELECT table_name FROM information_schema.tables WHERE table_schema='sdlc'` → 4개 테이블 확인
- [ ] `python .github/scripts/migrate_tasks_to_postgres.py --dry-run` — 21개 job 파싱 성공
- [ ] `dotnet test PlatformA.sln` — 기존 133개 테스트 전체 통과
