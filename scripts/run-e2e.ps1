# Usage: .\scripts\run-e2e.ps1 [-Scenario <number>]
# Scenario: 9 (default), all
param(
    [string]$Scenario = "9"
)

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $RepoRoot "PlatformA\PlatformA.Game.DummyClient"

Write-Host "[run-e2e.ps1] 시나리오 $Scenario 실행 시작"
Write-Host "[run-e2e.ps1] 프로젝트: $Project"

dotnet run --project $Project -- --e2e $Scenario
$ExitCode = $LASTEXITCODE

if ($ExitCode -eq 0) {
    Write-Host "[run-e2e.ps1] ✅ E2E 시나리오 ${Scenario}: SUCCESS"
} else {
    Write-Host "[run-e2e.ps1] ❌ E2E 시나리오 ${Scenario}: FAILURE (exit $ExitCode)"
}

exit $ExitCode
