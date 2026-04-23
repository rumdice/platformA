#!/bin/bash
# SessionStart hook: 세션 시작 시 SPRINT 현황 + 빌드 상태 출력
# 웹/원격 환경에서만 실행 (로컬은 skip)

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
    exit 0
fi

PROJECT_DIR="/home/user/platformA"
SLN_DIR="$PROJECT_DIR/PlatformA"
SPRINT_FILE="$PROJECT_DIR/AI/SPRINT.md"

echo ""
echo "============================================"
echo " PlatformA — 세션 시작 체크"
echo "============================================"

# 1. SPRINT.md 현황 출력
if [ -f "$SPRINT_FILE" ]; then
    echo ""
    echo "[SPRINT 현황]"
    # 진행 중 항목 추출
    IN_PROGRESS=$(grep -A 20 "## 진행 중" "$SPRINT_FILE" | grep -E "^\- \[ \]" | head -10)
    COMPLETED=$(grep -E "^\- \[x\]" "$SPRINT_FILE" | tail -5)

    if [ -n "$IN_PROGRESS" ]; then
        echo "  진행 중:"
        echo "$IN_PROGRESS" | sed 's/^/    /'
    else
        echo "  진행 중인 태스크 없음"
    fi

    if [ -n "$COMPLETED" ]; then
        echo "  최근 완료:"
        echo "$COMPLETED" | sed 's/^/    /'
    fi
else
    echo "[경고] AI/SPRINT.md 파일을 찾을 수 없습니다."
fi

# 2. 빌드 상태 확인
echo ""
echo "[빌드 상태 확인]"
if command -v dotnet &>/dev/null; then
    cd "$SLN_DIR" || exit 0
    BUILD_OUTPUT=$(dotnet build PlatformA.sln -q 2>&1)
    BUILD_EXIT=$?
    if [ $BUILD_EXIT -eq 0 ]; then
        echo "  ✔ dotnet build — 성공"
    else
        echo "  ✘ dotnet build — 실패"
        echo "$BUILD_OUTPUT" | grep -E "error|Error" | head -10 | sed 's/^/    /'
    fi
else
    echo "  [skip] dotnet 명령을 찾을 수 없습니다."
fi

echo ""
echo "============================================"
echo ""
