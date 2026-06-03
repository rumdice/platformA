#Requires -Version 5.0
# PlatformA AI_SDLC Phase 3 인프라 원터치 시작 스크립트 (Windows PowerShell)
# 실행: .\docker\sdlc\setup-sdlc.ps1
$ErrorActionPreference = "Stop"

$ScriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$EnvFile    = Join-Path $ScriptDir ".env"
$EnvExample = Join-Path $ScriptDir ".env.example"

Write-Host "[SDLC Setup] PlatformA AI_SDLC Phase 3 인프라를 초기화합니다." -ForegroundColor Cyan

# 1. .env 파일 생성 (없는 경우에만)
if (-not (Test-Path $EnvFile)) {
    if (Test-Path $EnvExample) {
        Copy-Item $EnvExample $EnvFile
        Write-Host "[SDLC Setup] .env 파일 생성 완료. 필요 시 수정하세요: $EnvFile"
    } else {
        Write-Error "[Error] .env.example 파일을 찾을 수 없습니다: $EnvExample"
        exit 1
    }
} else {
    Write-Host "[SDLC Setup] .env 파일이 이미 존재합니다: $EnvFile"
}

# 2. Docker Compose 기동
Write-Host "[SDLC Setup] PostgreSQL + n8n 컨테이너를 시작합니다..."
docker compose -f "$ScriptDir\docker-compose.yml" up -d

# 3. 상태 확인
Write-Host ""
Write-Host "[SDLC Setup] 컨테이너 상태:"
docker compose -f "$ScriptDir\docker-compose.yml" ps

Write-Host ""
Write-Host "[SDLC Setup] 완료!" -ForegroundColor Green
Write-Host ""
Write-Host "  PostgreSQL: localhost:5432"
Write-Host "    psql -h localhost -p 5432 -U platforma -d platforma_sdlc"
Write-Host ""
Write-Host "  n8n UI: http://localhost:5678"
Write-Host "    기본 계정: admin / admin_dev_password (.env에서 변경 가능)"
Write-Host ""
Write-Host "  종료:   docker compose -f docker/sdlc/docker-compose.yml down"
Write-Host "  초기화: docker compose -f docker/sdlc/docker-compose.yml down -v"
Write-Host ""
