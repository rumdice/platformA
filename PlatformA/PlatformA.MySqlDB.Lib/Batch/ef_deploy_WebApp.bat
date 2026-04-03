@echo off
:: ============================================================
:: [배포용] db_WebApp 마이그레이션 적용 — 기존 데이터 보존
::
:: 역할:
::   - 현재 DB에 아직 적용되지 않은 마이그레이션만 순차 적용합니다.
::   - DB 삭제 없이 안전하게 스키마를 최신 상태로 업그레이드합니다.
::   - 최초 설치(DB가 없는 경우)에도 사용 가능합니다.
::
:: 사용 흐름:
::   1) 아래 "연결 정보" 섹션을 본인 환경에 맞게 수정합니다.
::   2) dotnet-ef 도구가 설치되어 있는지 확인합니다.
::      (없으면: dotnet tool install --global dotnet-ef)
::   3) 이 스크립트를 실행합니다.
::
:: 적용 대상 테이블: players, player_stats, match_records
:: ============================================================

:: ── 연결 정보 (환경에 맞게 수정하세요) ──────────────────────
set DB_SERVER=localhost
set DB_PORT=3306
set DB_NAME=db_WebApp
set DB_USER=root
set DB_PASS=pass1234
:: ─────────────────────────────────────────────────────────────

set WEBAPP_DB_CONNECTION=Server=%DB_SERVER%;Port=%DB_PORT%;Database=%DB_NAME%;User=%DB_USER%;Password=%DB_PASS%

cd /d "%~dp0.."
chcp 65001 >nul

:: Migrations 폴더 존재 확인
if not exist "Migrations\WebApp" (
    echo.
    echo [ERROR] Migrations\WebApp 폴더가 없습니다.
    echo         아직 마이그레이션이 생성된 적 없는 환경입니다.
    echo.
    echo         해결 방법: ef_create_database_CF_WebApp.bat 을 먼저 실행하세요.
    echo         (주의: 해당 스크립트는 기존 DB를 삭제합니다. 신규 설치 환경에서만 사용하세요.)
    echo.
    pause
    exit /b 1
)

echo.
echo [WebApp Deploy] 연결 대상: %DB_SERVER%:%DB_PORT%/%DB_NAME%
echo [WebApp Deploy] 미적용 마이그레이션 적용 중...
dotnet ef database update ^
  --context DbWebAppContext ^
  --project . --startup-project .

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] 마이그레이션 적용 실패. 위 오류 메시지를 확인하세요.
    pause
    exit /b 1
)

echo.
echo [WebApp Deploy] 완료. 모든 마이그레이션이 적용되었습니다.
pause
