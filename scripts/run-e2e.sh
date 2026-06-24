#!/usr/bin/env bash
# Usage: ./scripts/run-e2e.sh [scenario]
# scenario: 9 (default), all
set -e

SCENARIO=${1:-9}
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

echo "[run-e2e.sh] 시나리오 ${SCENARIO} 실행 시작"
echo "[run-e2e.sh] 프로젝트: ${REPO_ROOT}/PlatformA/PlatformA.Game.DummyClient"

dotnet run --project "${REPO_ROOT}/PlatformA/PlatformA.Game.DummyClient" -- --e2e "${SCENARIO}"
EXIT_CODE=$?

if [ $EXIT_CODE -eq 0 ]; then
    echo "[run-e2e.sh] ✅ E2E 시나리오 ${SCENARIO}: SUCCESS"
else
    echo "[run-e2e.sh] ❌ E2E 시나리오 ${SCENARIO}: FAILURE (exit ${EXIT_CODE})"
fi

exit $EXIT_CODE
