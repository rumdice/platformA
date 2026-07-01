# Gomoku E2E 자동화 — CLI 모드 + 로그 + 실행 스크립트

## 작업 목적
`TwoPlayerGomokuScenario`를 CI/스크립트에서 자동으로 실행할 수 있도록 DummyClient에
non-interactive 모드를 추가하고, 실행 결과를 exit code와 로그 파일로 남긴다.

## 상세 요구사항

### P0 — Non-interactive CLI 모드

**Program.cs 수정**:
- `args`에 `--e2e <번호>` 패턴 감지
  - 예: `dotnet run -- --e2e 9`
  - Interactive 메뉴를 건너뛰고 해당 시나리오만 실행
  - 성공 시 `Environment.Exit(0)`, 실패 시 `Environment.Exit(1)`
- `--e2e all`: 등록된 모든 E2E 시나리오를 순서대로 실행 (현재는 9번만)
- `--list`: 사용 가능한 시나리오 목록 출력 후 종료

**로그 파일 저장**:
- 실행 시 `logs/e2e-{yyyyMMdd-HHmmss}.log` 파일을 자동 생성
- Console 출력을 파일에도 동시 기록 (`TeeWriter` 구현: TextWriter를 래핑하여 Console+File 동시 출력)
- 마지막 줄에 `[RESULT] SUCCESS` 또는 `[RESULT] FAILURE: {이유}` 기록

### P1 — 실행 스크립트

**`scripts/run-e2e.sh`** (Bash):
```bash
#!/usr/bin/env bash
set -e
SCENARIO=${1:-9}
LOG_DIR="logs/e2e"
mkdir -p "$LOG_DIR"
dotnet run --project PlatformA/PlatformA.Game.DummyClient -- --e2e "$SCENARIO"
EXIT_CODE=$?
echo "E2E 시나리오 ${SCENARIO} 결과: $([ $EXIT_CODE -eq 0 ] && echo SUCCESS || echo FAILURE)"
exit $EXIT_CODE
```

**`scripts/run-e2e.ps1`** (PowerShell — Windows 호환):
동일한 역할의 PowerShell 버전

### P2 — 시나리오 결과 판단 개선

**TwoPlayerGomokuScenario.cs 수정**:
- 현재 `task1.IsFaulted || task2.IsFaulted`로만 판단 → 명시적 결과 반환 타입 추가
- 반환: `bool success` → 상위에서 exit code로 변환
- 타임아웃(120초 전체 CTS)에서도 실패로 처리

## 실행 후 검증 (Sprint 내 E2E 실행 포함)

구현 완료 후 실제로 다음을 실행하고 결과를 리포트에 기록:
```bash
# 로컬 서비스가 모두 기동된 상태에서
dotnet run --project PlatformA/PlatformA.Game.DummyClient -- --e2e 9
```
- 성공/실패 여부
- 로그 파일 내용 요약
- 발견된 문제점

## 제약 및 주의사항
- 서비스가 기동되지 않은 상태에서 --e2e 실행 시: 연결 실패 오류 → exit 1 (예외 아님)
- 계정명은 기존 `GenerateTestUserName()` 유지 (8자리 랜덤 — 반복 실행 안전)
- docker-compose 기반 전체 환경 자동화는 이 스프린트에서 제외 (차후 Sprint)
- `logs/` 디렉토리는 `.gitignore`에 추가

## 검증 기준
- `dotnet run -- --e2e 9` 실행 후 exit code 0 (성공) 또는 1 (실패) 반환
- `logs/e2e-*.log` 파일 생성 확인
- `dotnet run -- --list` 시나리오 목록 출력 확인
- 전체 빌드/테스트(`dotnet test`) 통과
