#!/bin/bash
# SessionStart hook: 세션 시작 시 SPRINT 현황 + 빌드 상태 출력

# 프로젝트 루트를 git 기반으로 탐색 (환경 무관)
PROJECT_DIR="$(git rev-parse --show-toplevel 2>/dev/null)"
if [ -z "$PROJECT_DIR" ]; then
    exit 0
fi

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

# 2. 브랜치 및 환경 정보
echo ""
echo "[환경 정보]"
echo "  브랜치: $(git branch --show-current 2>/dev/null || echo '알 수 없음')"
echo "  환경:   ${CLAUDE_CODE_REMOTE:+웹(원격)}${CLAUDE_CODE_REMOTE:-로컬}"
echo "  루트:   $PROJECT_DIR"

# 3. 빌드 상태 확인
echo ""
echo "[빌드 상태 확인]"
if command -v dotnet &>/dev/null && [ -d "$SLN_DIR" ]; then
    cd "$SLN_DIR" || exit 0
    BUILD_OUTPUT=$(dotnet build PlatformA.sln --verbosity minimal 2>&1)
    BUILD_EXIT=$?
    if [ $BUILD_EXIT -eq 0 ]; then
        echo "  ✔ dotnet build — 성공"
    else
        echo "  ✘ dotnet build — 실패"
        echo "$BUILD_OUTPUT" | grep -E "error CS|error NU|error MSB[^4]" | head -10 | sed 's/^/    /'
    fi
else
    echo "  [skip] dotnet 명령 또는 솔루션 디렉터리를 찾을 수 없습니다."
fi


# 4. 미해결 CI 실패 조회 (PostgreSQL ai_failures, psycopg2 설치 시)
CURRENT_BRANCH=$(git branch --show-current 2>/dev/null)
if [ -n "$CURRENT_BRANCH" ] && [ "$CURRENT_BRANCH" != "main" ]; then
    FAILURE_OUTPUT=$(python3 "$PROJECT_DIR/.github/scripts/record_failure.py" \
        --list-unresolved --branch "$CURRENT_BRANCH" 2>/dev/null)
    if echo "$FAILURE_OUTPUT" | grep -q "미해결 CI 실패"; then
        echo ""
        echo "[CI 실패 알림]"
        echo "$FAILURE_OUTPUT" | sed 's/^/  /'
        echo "  → 수정 후 /done 을 재실행하세요."
    fi
fi

echo ""
echo "============================================"
echo ""
