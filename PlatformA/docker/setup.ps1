#Requires -Version 5.0
# PlatformA Docker 환경 최초 설정 스크립트 (Windows PowerShell)
# 실행: .\docker\setup.ps1
$ErrorActionPreference = "Stop"

$ScriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$CertsDir    = Join-Path $ScriptDir "certs"
$CertFile    = Join-Path $CertsDir "devcert.pfx"
$CertPassword = if ($env:CERT_PASSWORD) { $env:CERT_PASSWORD } else { "localdev" }

Write-Host "[Setup] PlatformA Docker 환경을 초기화합니다." -ForegroundColor Cyan

# 1. certs 디렉터리 생성
if (-not (Test-Path $CertsDir)) {
    New-Item -ItemType Directory -Path $CertsDir | Out-Null
}

# 2. 개발 인증서 생성 (이미 있으면 건너뜀)
if (Test-Path $CertFile) {
    Write-Host "[Setup] 개발 인증서가 이미 존재합니다: $CertFile"
    Write-Host "[Setup] 재생성하려면 파일을 삭제 후 다시 실행하세요."
} else {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Error "[Error] dotnet SDK가 설치되지 않았습니다. https://dotnet.microsoft.com/download 에서 .NET 10 SDK를 설치하세요."
        exit 1
    }
    Write-Host "[Setup] 개발 인증서를 생성합니다..."
    try { dotnet dev-certs https --trust 2>$null } catch {}
    dotnet dev-certs https --export-path $CertFile --password $CertPassword
    Write-Host "[Setup] 인증서 생성 완료: $CertFile" -ForegroundColor Green
}

# 3. .env 파일 생성 (없는 경우에만)
$EnvFile    = Join-Path $ScriptDir ".env"
$EnvExample = Join-Path $ScriptDir ".env.example"
if (-not (Test-Path $EnvFile) -and (Test-Path $EnvExample)) {
    Copy-Item $EnvExample $EnvFile
    Write-Host "[Setup] .env 파일 생성 완료. 필요 시 수정하세요: $EnvFile"
}

$ProjectDir = Split-Path -Parent $ScriptDir
Write-Host ""
Write-Host "[Setup] 완료! 이제 다음 명령어로 전체 스택을 시작하세요:" -ForegroundColor Green
Write-Host ""
Write-Host "  cd $ProjectDir"
Write-Host "  docker compose -f docker/docker-compose.full.yml up -d --build"
Write-Host ""
