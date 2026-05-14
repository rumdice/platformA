# PlatformA 문서 로컬 서버 관리 스크립트
# 사용법: .\Docs\serve-local.ps1 [start|stop|restart|build]

param([string]$Action = "start")

$SitePath = Join-Path $PSScriptRoot "_site"
$ServePath = "$env:USERPROFILE\.dotnet\tools\dotnet-serve.exe"
$DocfxPath = "$env:USERPROFILE\.dotnet\tools\docfx.exe"
$RootPath = Split-Path $PSScriptRoot

function Stop-Server {
    $conn = Get-NetTCPConnection -LocalPort 8080 -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($conn) {
        Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue
        Write-Host "[docs] localhost:8080 종료됨"
    } else {
        Write-Host "[docs] 실행 중인 서버 없음"
    }
}

function Start-Server {
    if (-not (Test-Path $SitePath)) {
        Write-Host "[docs] _site 없음 — 먼저 빌드합니다..."
        Build-Docs
    }
    Write-Host "[docs] http://localhost:8080 시작 중..."
    & $ServePath --directory $SitePath --port 8080 --open-browser
}

function Build-Docs {
    Write-Host "[docs] DocFX 빌드 중..."
    Remove-Item -Recurse -Force $SitePath -ErrorAction SilentlyContinue
    & $DocfxPath (Join-Path $PSScriptRoot "docfx.json")
    if ($LASTEXITCODE -eq 0) {
        Write-Host "[docs] 빌드 완료"
    } else {
        Write-Host "[docs] 빌드 실패 (exit $LASTEXITCODE)"
        exit 1
    }
}

switch ($Action) {
    "stop"    { Stop-Server }
    "start"   { Stop-Server; Start-Server }
    "restart" { Stop-Server; Start-Server }
    "build"   { Build-Docs }
    "build-serve" { Build-Docs; Stop-Server; Start-Server }
    default   { Write-Host "사용법: .\serve-local.ps1 [start|stop|restart|build|build-serve]" }
}
