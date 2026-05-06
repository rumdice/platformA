---
description: EF Core 마이그레이션 및 DB 스키마 변경 규칙
globs: ["PlatformA/PlatformA.MySqlDB.Lib/**"]
---

# EF Core 마이그레이션 규칙

## 절대 금지
- Migration 없이 DB 스키마 변경 절대 금지
- 직접 SQL 실행 금지 (`context.Database.ExecuteSqlRaw(...)` 포함)
- 수동 스키마 변경 (`ALTER TABLE` 등) 금지

## 네이밍 컨벤션
- 테이블명: `snake_case` (예: `match_records`, `player_stats`)
- 컬럼명: `snake_case`
- Migration 이름: PascalCase 동사+명사 (예: `AddRatingColumn`, `CreateMatchRecordsTable`)

## DbContext 구분
| Context | 용도 | Migration 경로 |
|---------|------|---------------|
| `DbWebAppContext` | 게임 플레이어/아이템/매칭 | `Migrations/WebApp` |
| `DbLogAppContext` | 접속 로그 | `Migrations/LogApp` |

## Migration 생성 명령
```bash
cd PlatformA/PlatformA.MySqlDB.Lib
dotnet ef migrations add <이름> --context DbWebAppContext --output-dir Migrations/WebApp
dotnet ef migrations add <이름> --context DbLogAppContext --output-dir Migrations/LogApp
```

## Migration 적용 전 필수 확인
- `Up()` 메서드: 적용될 변경 사항 검토
- `Down()` 메서드: 롤백 가능 여부 확인
- 기존 데이터 영향 분석 (NOT NULL 컬럼 추가 시 기본값 필요)
- `/migrate` 스킬 또는 `db-migrator` 에이전트를 통해 안전하게 적용
