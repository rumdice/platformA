#!/bin/bash
# PreToolUse hook: git push 전 dotnet build 통과 여부 검증
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

# 전체 솔루션 빌드 실행
cd "$SLN_DIR" || exit 0
BUILD_OUTPUT=$(dotnet build PlatformA.sln -q 2>&1)
BUILD_EXIT=$?

if [ $BUILD_EXIT -ne 0 ]; then
    echo "{\"decision\": \"block\", \"reason\": \"빌드 실패: git push가 차단됩니다.\\n\\n${BUILD_OUTPUT}\\n\\ndotnet build PlatformA.sln 오류를 수정한 뒤 다시 push하십시오.\"}"
    exit 0
fi

# 코드 포맷 검사
FORMAT_OUTPUT=$(dotnet format PlatformA.sln --verify-no-changes --no-restore 2>&1)
FORMAT_EXIT=$?

if [ $FORMAT_EXIT -ne 0 ]; then
    echo "{\"decision\": \"block\", \"reason\": \"코드 포맷 불일치: git push가 차단됩니다.\\n\\n${FORMAT_OUTPUT}\\n\\ndotnet format PlatformA/PlatformA.sln 실행 후 다시 push하십시오.\"}"
    exit 0
fi

# 빌드 + 포맷 모두 통과 → push 허용
exit 0
