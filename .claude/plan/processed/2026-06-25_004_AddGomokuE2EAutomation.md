# 요구사항 명세: AddGomokuE2EAutomation

작성일: 2026-06-25
브랜치: 2026-06-25_AddGomokuE2EAutomation
소스: .claude/plan/2026-06-25_GomokuE2EAutomation.md

## 요구사항 요약

DummyClient에 non-interactive CLI 모드(`--e2e`)를 추가하여 오목 E2E 시나리오를 스크립트/CI에서
자동 실행할 수 있게 한다. 실행 결과는 exit code(0=성공, 1=실패)와 타임스탬프 로그 파일로 남긴다.

## 상세 요구사항

### P0 — Non-interactive CLI 모드 (Program.cs)

1. `args`에서 `--e2e <번호>` 패턴 감지
   - 예: `dotnet run -- --e2e 9`
   - Interactive 메뉴를 건너뛰고 해당 시나리오만 실행
   - 성공 시 `Environment.Exit(0)`, 실패 시 `Environment.Exit(1)`
2. `--e2e all` — 등록된 모든 E2E 시나리오를 순서대로 실행 (현재는 9번만)
3. `--list` — 사용 가능한 시나리오 목록 출력 후 종료 (exit 0)

### P0 — TeeWriter (Console + File 동시 출력)

- `TeeWriter : TextWriter` 구현 — 내부에 `Console.Out`과 `StreamWriter`(파일)를 동시 write
- `Program.cs` 진입 시 `logs/e2e-{yyyyMMdd-HHmmss}.log` 파일 생성
- `Console.SetOut(new TeeWriter(...))` 로 교체
- 마지막 줄에 `[RESULT] SUCCESS` 또는 `[RESULT] FAILURE: {이유}` 기록

### P1 — 실행 스크립트

**`scripts/run-e2e.sh`** (Bash, 실행 권한 필요):
```bash
#!/usr/bin/env bash
set -e
SCENARIO=${1:-9}
dotnet run --project PlatformA/PlatformA.Game.DummyClient -- --e2e "$SCENARIO"
EXIT_CODE=$?
echo "E2E 시나리오 ${SCENARIO}: $([ $EXIT_CODE -eq 0 ] && echo SUCCESS || echo FAILURE)"
exit $EXIT_CODE
```

**`scripts/run-e2e.ps1`** (PowerShell — Windows 호환):
```powershell
param([string]$Scenario = "9")
dotnet run --project PlatformA/PlatformA.Game.DummyClient -- --e2e $Scenario
$exitCode = $LASTEXITCODE
Write-Host "E2E 시나리오 ${Scenario}: $(if ($exitCode -eq 0) { 'SUCCESS' } else { 'FAILURE' })"
exit $exitCode
```

### P2 — TwoPlayerGomokuScenario 명시적 결과 반환

- `RunAsync()` 반환 타입을 `Task` → `Task<bool>` 로 변경 (`true`=성공, `false`=실패)
- 타임아웃(120초 CTS) 만료 시 `false` 반환
- `task1.IsFaulted || task2.IsFaulted` → 예외 포함 모든 실패 케이스 `false`

### P0 — .gitignore

- `PlatformA/PlatformA.Game.DummyClient/logs/` 를 `.gitignore`에 추가

## 영향 범위 (예상)

| 파일 | 변경 내용 |
|------|---------|
| `PlatformA.Game.DummyClient/Program.cs` | CLI 인수 파싱, TeeWriter 초기화, E2E 라우팅 |
| `PlatformA.Game.DummyClient/Scenarios/TwoPlayerGomokuScenario.cs` | `Task<bool>` 반환으로 변경 |
| `PlatformA.Game.DummyClient/TeeWriter.cs` (신규) | TextWriter 래퍼 |
| `scripts/run-e2e.sh` (신규) | bash 실행 스크립트 |
| `scripts/run-e2e.ps1` (신규) | PowerShell 실행 스크립트 |
| `.gitignore` | logs/ 디렉토리 제외 |

## 제약 및 주의사항

- `GenerateTestUserName()` 유지 — 8자리 랜덤, 반복 실행 안전
- 서비스 미기동 상태에서 실행 시 연결 실패 → exit 1 (예외 아님)
- `logs/` 디렉토리는 DummyClient 실행 시 자동 생성 (`Directory.CreateDirectory`)
- docker-compose 전체 환경 자동화는 이번 스프린트에서 제외
- TeeWriter는 `Dispose()` 시 파일 스트림만 닫는다 (Console.Out는 닫지 않음)

## 구현 접근 방향

1. `TeeWriter.cs` 신규 파일로 분리 — `Write(char)` 오버라이드만으로 충분
2. `Program.cs` 상단에서 args 파싱 → `--e2e`, `--list` 감지 후 분기
3. `TwoPlayerGomokuScenario.RunAsync()` signature만 변경 (로직은 유지)
4. `scripts/` 디렉토리를 repo 루트에 생성 (DummyClient 디렉토리 아님)

## 검증 기준

- `dotnet run --project PlatformA/PlatformA.Game.DummyClient -- --list` → 시나리오 목록 출력, exit 0
- `dotnet run --project PlatformA/PlatformA.Game.DummyClient -- --e2e 9` → 서비스 기동 시 exit 0, 미기동 시 exit 1
- `PlatformA.Game.DummyClient/logs/e2e-*.log` 파일 생성 확인
- `dotnet build PlatformA.sln` 오류 0개
- `dotnet test PlatformA.sln` 전체 통과 (기존 212개 유지)
