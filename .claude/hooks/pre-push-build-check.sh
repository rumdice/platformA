#!/bin/bash
# PreToolUse hook: git push 전 브랜치·빌드·포맷 검증
# stdin: 도구 호출 정보 (JSON)
# 출력: {"decision": "block", "reason": "..."} → 차단 / 출력 없음 → 허용

INPUT=$(cat)

# 실행하려는 명령어 추출
COMMAND=$(echo "$INPUT" | python3 -c "
import sys, json
try:
    data = json.load(sys.stdin)
    print(data.get('tool_input', {}).get('command', ''))
except:
    print('')
" 2>/dev/null)

# git push 명령인지 확인
if ! echo "$COMMAND" | grep -qE "git push"; then
    exit 0
fi

# ── 1. main 브랜치 직접 push 차단 ────────────────────────────────────────
CURRENT_BRANCH=$(git branch --show-current 2>/dev/null)
if [ "$CURRENT_BRANCH" = "main" ]; then
    echo '{"decision": "block", "reason": "main 브랜치에 직접 push할 수 없습니다.\n/plan을 먼저 실행하여 작업 브랜치를 생성한 뒤 push하십시오."}'
    exit 0
fi

# ── 2. /done 마커 확인 — 이미 검증됐으면 재검사 스킵 ───────────────────────
MARKER="/tmp/.platformA_done_verified"
if [ -f "$MARKER" ]; then
    MARKER_AGE=$(( $(date +%s) - $(cat "$MARKER" 2>/dev/null || echo 0) ))
    if [ "$MARKER_AGE" -lt 300 ]; then
        rm -f "$MARKER"
        exit 0  # /done이 300초 이내에 build+format+test 통과 확인
    fi
    rm -f "$MARKER"
fi

# dotnet 사용 가능 여부 확인
if ! command -v dotnet &>/dev/null; then
    exit 0
fi

# 프로젝트 루트를 git 기반으로 탐색 (환경 무관)
PROJECT_DIR="$(git rev-parse --show-toplevel 2>/dev/null)"
if [ -z "$PROJECT_DIR" ]; then
    exit 0
fi

SLN_DIR="$PROJECT_DIR/PlatformA"
if [ ! -d "$SLN_DIR" ]; then
    exit 0
fi

cd "$SLN_DIR" || exit 0

# ── 3. 전체 솔루션 빌드 ───────────────────────────────────────────────────
BUILD_OUTPUT=$(dotnet build PlatformA.sln -q 2>&1)
BUILD_EXIT=$?

if [ $BUILD_EXIT -ne 0 ]; then
    echo "{\"decision\": \"block\", \"reason\": \"빌드 실패: git push가 차단됩니다.\\n\\n${BUILD_OUTPUT}\\n\\ndotnet build PlatformA.sln 오류를 수정한 뒤 다시 push하십시오.\"}"
    exit 0
fi

# ── 4. 포맷 검사 (CI와 동일하게 whitespace → style 순서) ─────────────────
WS_OUTPUT=$(dotnet format PlatformA.sln whitespace --verify-no-changes --no-restore 2>&1)
WS_EXIT=$?

if [ $WS_EXIT -ne 0 ]; then
    echo "{\"decision\": \"block\", \"reason\": \"공백 포맷 불일치: git push가 차단됩니다.\\n\\n${WS_OUTPUT}\\n\\ndotnet format PlatformA.sln whitespace --no-restore 실행 후 다시 push하십시오.\"}"
    exit 0
fi

STYLE_OUTPUT=$(dotnet format PlatformA.sln style --verify-no-changes --no-restore 2>&1)
STYLE_EXIT=$?

if [ $STYLE_EXIT -ne 0 ]; then
    echo "{\"decision\": \"block\", \"reason\": \"스타일 포맷 불일치: git push가 차단됩니다.\\n\\n${STYLE_OUTPUT}\\n\\ndotnet format PlatformA.sln style --no-restore 실행 후 다시 push하십시오.\"}"
    exit 0
fi

# 모든 검사 통과 → push 허용
exit 0
