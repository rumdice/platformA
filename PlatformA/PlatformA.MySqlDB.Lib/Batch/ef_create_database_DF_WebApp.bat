@echo off
:: ============================================================
:: Database-First : DB 현재 상태를 C# 엔티티로 역방향 생성합니다.
:: ※ CF에서 수동 작성한 엔티티(Navigation 등)를 보호하기 위해
::   Scaffold 후 수동 머지가 필요합니다.
:: ============================================================

cd /d "%~dp0.."
chcp 65001 >nul

:: ★ 1. 기존 엔티티 백업 (CF에서 손으로 추가한 Navigation 등 보존용)
echo [WebApp DF] 기존 엔티티 백업...
if not exist "DBWebApp\Entities\_backup" mkdir "DBWebApp\Entities\_backup"
xcopy /Y "DBWebApp\Entities\*.cs" "DBWebApp\Entities\_backup\"

:: ★ 2. Scaffold 실행 (전체 테이블 대상)
echo [WebApp DF] DB에서 엔티티 역방향 생성 (Database-First)...
dotnet ef dbcontext scaffold ^
  "Server=localhost;Port=3306;Database=db_WebApp;User=root;Password=pass1234" ^
  Pomelo.EntityFrameworkCore.MySql ^
  --output-dir DBWebApp/Entities ^
  --force ^
  --context DbWebAppContext ^
  --use-database-names ^
  --no-pluralize ^
  --no-onconfiguring ^
  --no-build ^
  --project . ^
  --startup-project .

:: ★ 3. Scaffold가 생성한 중복 Context 파일 삭제
echo [WebApp DF] 중복 Context 파일 제거...
if exist "DBWebApp\Entities\DbWebAppContext.cs" del "DBWebApp\Entities\DbWebAppContext.cs"

echo.
echo ============================================================
echo [주의] Scaffold 완료. 아래 후처리를 반드시 수행하세요:
echo.
echo   1. DBWebApp\Entities\_backup\ 과 현재 파일을 비교(diff)
echo      → Navigation Property, Collection 초기화 등 수동 복원
echo   2. DbWebAppContext.cs의 OnModelCreating Fluent API 갱신
echo   3. 빈 Migration 생성으로 스냅샷 동기화:
echo      dotnet ef migrations add SyncFromDb --context DbWebAppContext --output-dir Migrations/WebApp
echo ============================================================
echo.

echo [WebApp DF] 빌드 확인...
dotnet build

echo [WebApp DF] 완료.
pause