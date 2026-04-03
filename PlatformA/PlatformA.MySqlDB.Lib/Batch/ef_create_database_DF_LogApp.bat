@echo off
:: ============================================================
:: Database-First : 이미 존재하는 DB에서 엔티티를 역방향으로 생성합니다.
::
:: 사용 흐름:
::   1) MariaDB에 db_LogApp 스키마와 테이블이 미리 존재해야 합니다.
::   2) 이 스크립트 실행 → DBLogApp\ 폴더에 엔티티 파일 자동 생성
:: ============================================================

cd /d "%~dp0.."
chcp 65001 >nul

echo [LogApp DF] DB에서 엔티티 역방향 생성 (Database-First)...
dotnet ef dbcontext scaffold ^
  "Server=localhost;Port=3306;Database=db_LogApp;User=root;Password=pass1234" ^
  Pomelo.EntityFrameworkCore.MySql ^
  --output-dir DBLogApp ^
  --force ^
  --context DbLogAppContext ^
  --use-database-names ^
  --no-pluralize ^
  --no-onconfiguring ^
  --project . ^
  --startup-project .

echo [LogApp DF] 빌드 확인...
dotnet build

echo [LogApp DF] 완료.
