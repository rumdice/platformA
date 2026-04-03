@echo off
:: ============================================================
:: Database-First : 이미 존재하는 DB에서 엔티티를 역방향으로 생성합니다.
::
:: 사용 흐름:
::   1) MariaDB에 db_WebApp 스키마와 테이블이 미리 존재해야 합니다.
::   2) 이 스크립트 실행 → DBWebApp\ 폴더에 엔티티 파일 자동 생성
::
:: --no-onconfiguring : OnConfiguring 제거 (DI 주입 방식 사용)
:: --no-pluralize     : 테이블명 복수화 비활성화 (원본 이름 유지)
:: --use-database-names : DB 컬럼명을 그대로 프로퍼티 이름으로 사용
:: ============================================================

cd /d "%~dp0.."
chcp 65001 >nul

echo [WebApp DF] DB에서 엔티티 역방향 생성 (Database-First)...
dotnet ef dbcontext scaffold ^
  "Server=localhost;Port=3306;Database=db_WebApp;User=root;Password=pass1234" ^
  Pomelo.EntityFrameworkCore.MySql ^
  --output-dir DBWebApp/Entities
  --force ^
  --context DbWebAppContext ^
  --use-database-names ^
  --no-pluralize ^
  --no-onconfiguring ^
  --project . ^
  --startup-project .

echo [WebApp DF] 빌드 확인...
dotnet build

echo [WebApp DF] 완료.

pause