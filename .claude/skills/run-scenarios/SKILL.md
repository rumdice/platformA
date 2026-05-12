---
name: run-scenarios
description: 시나리오 6(미구현)을 제외한 모든 DummyClient 시나리오(1~5, 7, 8)를 자동으로 실행한다. 인프라 체크 → 기존 서버 강제 종료 → 서버 자동 시작 → stdin 파이핑 실행 → Redis 정리 → 서버 종료 → 콘솔 리포트 순서로 진행한다.
allowed-tools: Bash(docker *) Bash(dotnet *) Bash(curl *) Bash(printf *) Bash(timeout *) Bash(kill *) Bash(powershell *) Read
---

# DummyClient 시나리오 전체 실행

## 전제 조건
- Redis 클러스터 (`redis-master-1~3`) Docker 컨테이너가 **이미 실행 중**이어야 한다.
- MariaDB (`localhost:3306`)가 **이미 실행 중**이어야 한다.
- 두 인프라 중 하나라도 응답하지 않으면 **경고 후 즉시 중단**한다.
- DB에 테스트 계정 `lt_0001` ~ `lt_1000` (비밀번호: `123456`)이 등록되어 있어야 한다.

## 경로 설정

```bash
SLN=$(git rev-parse --show-toplevel)/PlatformA
DUMMY="$SLN/PlatformA.Game.DummyClient"
AUTH_DIR="$SLN/PlatformA.Auth.API"
TICKET_DIR="$SLN/PlatformA.Ticketing.API"
MATCH_DIR="$SLN/PlatformA.Matching.API"
GAME_DIR="$SLN/PlatformA.Game.Server"
```

---

## 수행 순서

### 1단계: 인프라 체크

Redis 클러스터가 실행 중이지 않거나 응답이 없으면 즉시 중단한다.

```bash
# Redis 컨테이너 실행 여부
docker ps --filter "name=redis-master-1" --filter "status=running" -q | grep -q . \
  || { echo "[오류] Redis 클러스터(redis-master-1)가 실행 중이지 않습니다. 'cd Redis && docker-compose up -d' 로 먼저 시작하세요."; exit 1; }

# Redis PING 응답
docker exec redis-master-1 redis-cli -h 127.0.0.1 -p 6371 PING 2>/dev/null | grep -q "PONG" \
  || { echo "[오류] Redis PING 실패. 클러스터 상태를 확인하세요."; exit 1; }

echo "[확인] Redis 클러스터 정상"

# MariaDB 포트 응답 체크 (PowerShell TcpClient)
DB_OK=$(powershell -c "try { \$c=New-Object Net.Sockets.TcpClient('localhost',3306); \$c.Close(); 'OK' } catch { 'FAIL' }" 2>/dev/null | tr -d '\r')
[ "$DB_OK" = "OK" ] || { echo "[오류] MariaDB(localhost:3306)가 실행 중이지 않습니다. Docker MySQL 컨테이너를 먼저 시작하세요."; exit 1; }

echo "[확인] MariaDB 정상"
```

### 2단계: 기존 서버 강제 종료

시나리오 실행 전 클린 상태를 보장하기 위해 이미 실행 중인 서버 프로세스를 강제 종료한다.

```bash
kill_if_running() {
  local NAME="$1"
  local PORT="$2"
  local PID
  PID=$(powershell -c "
    \$conn = Get-NetTCPConnection -LocalPort $PORT -State Listen -ErrorAction SilentlyContinue
    if (\$conn) { \$conn.OwningProcess } else { '' }
  " 2>/dev/null | tr -d '\r\n ')
  if [ -n "$PID" ]; then
    echo "[강제 종료] $NAME (PORT=$PORT, PID=$PID) 종료 중..."
    powershell -c "Stop-Process -Id $PID -Force -ErrorAction SilentlyContinue" 2>/dev/null
    echo "  [완료] PID=$PID 종료됨"
  else
    echo "[확인] $NAME (PORT=$PORT) 실행 중이지 않음"
  fi
}

kill_if_running "Auth.API"      7088
kill_if_running "Ticketing.API" 7075
kill_if_running "Matching.API"  7007
kill_if_running "Game.Server"   7777

# 포트가 완전히 해제될 때까지 대기
sleep 2
echo "[완료] 기존 서버 정리 완료"
```

### 3단계: .NET 서버 자동 시작

각 서버를 HTTP/TCP 포트로 체크하고, 응답이 없으면 배경 실행 후 최대 30초 대기한다.
이미 실행 중이면 시작을 건너뛴다. 종료는 7단계에서 포트 기반으로 일괄 처리한다.

```bash
# HTTP 서버 체크 헬퍼 (HTTPS 포함, 인증서 무시)
check_http() {
  curl -k -s -o /dev/null -w "%{http_code}" --max-time 2 "$1" 2>/dev/null
}

# TCP 포트 체크 헬퍼
check_tcp() {
  powershell -c "try { \$c=New-Object Net.Sockets.TcpClient('$1',$2); \$c.Close(); 'OK' } catch { 'FAIL' }" 2>/dev/null | tr -d '\r'
}

# 서버 시작 헬퍼: start_if_not_running <이름> <디렉토리> <로그> <체크명령> [추가 dotnet run 옵션]
start_if_not_running() {
  local NAME="$1" DIR="$2" LOG="$3" CHECK_CMD="$4" EXTRA_ARGS="${5:-}"
  local CODE
  eval "CODE=\$($CHECK_CMD)"
  if [ "$CODE" = "000" ] || [ "$CODE" = "FAIL" ]; then
    echo "[시작] $NAME 실행 중..."
    (cd "$DIR" && dotnet run $EXTRA_ARGS > "$LOG" 2>&1) &
    local PID=$!
    echo "  PID=$PID, 로그=$LOG"
    echo "  최대 30초 대기 중..."
    for i in $(seq 1 15); do
      sleep 2
      eval "CODE=\$($CHECK_CMD)"
      if [ "$CODE" != "000" ] && [ "$CODE" != "FAIL" ]; then
        echo "  [OK] $NAME 준비 완료 (${i}*2초)"
        break
      fi
    done
    if [ "$CODE" = "000" ] || [ "$CODE" = "FAIL" ]; then
      echo "  [경고] $NAME 시작 타임아웃. 계속 진행하지만 일부 시나리오가 실패할 수 있습니다."
    fi
  else
    echo "[확인] $NAME 이미 실행 중"
  fi
}

start_if_not_running \
  "Auth.API" "$AUTH_DIR" "/tmp/auth_api.log" \
  'check_http "https://localhost:7088/"'

start_if_not_running \
  "Ticketing.API" "$TICKET_DIR" "/tmp/ticket_api.log" \
  'check_http "https://localhost:7075/"'

start_if_not_running \
  "Matching.API" "$MATCH_DIR" "/tmp/match_api.log" \
  'check_http "https://localhost:7007/"' \
  "--launch-profile https"

start_if_not_running \
  "Game.Server" "$GAME_DIR" "/tmp/game_server.log" \
  'check_tcp "127.0.0.1" 7777'
```

### 4단계: DummyClient 사전 빌드

```bash
echo ""
echo "[빌드] DummyClient 사전 빌드 중..."
cd "$SLN" && dotnet build "PlatformA.Game.DummyClient/PlatformA.Game.DummyClient.csproj" -q \
  || { echo "[오류] DummyClient 빌드 실패. 빌드 오류를 먼저 수정하세요."; exit 1; }
echo "[완료] 빌드 성공"
```

### 5단계: 시나리오 실행

각 시나리오를 순서대로 실행하고 결과를 `/tmp/scenario_N.log`에 저장한다.
시나리오 3만 두 프로세스를 병렬로 실행한 후 `wait`으로 동기화한다.

**stdin 패턴 근거:**
- 시나리오 1: 메뉴 선택(`1`) → username → password → CMove 루프에서 `q` 입력 → 메뉴 `0` 종료
- 시나리오 2: 메뉴 선택(`2`) → 최종 ReadLine(빈 줄) → 메뉴 `0` 종료  
- 시나리오 3: 메뉴 선택(`3`) → username → password → 행동 `m`(매칭 등록) → `q`(종료) → 메뉴 `0`
- 시나리오 4: 메뉴 선택(`4`) → 최종 ReadLine(빈 줄) → 메뉴 `0`
- 시나리오 5: 메뉴 선택(`5`) → 최종 ReadLine(빈 줄) → 메뉴 `0`
- 시나리오 7: 메뉴 선택(`7`) → username → password → 최종 ReadLine(빈 줄) → 메뉴 `0`
- 시나리오 8: 메뉴 선택(`8`) → username → password → 최종 ReadLine(빈 줄) → 메뉴 `0`

```bash
# ── 실행 헬퍼 ──────────────────────────────────────────────────
run_scenario() {
  local NUM="$1"
  local INPUT="$2"
  local TIMEOUT_SEC="$3"
  local LOG="/tmp/scenario_${NUM}.log"

  echo ""
  echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  echo "  [시나리오 $NUM] 실행 중... (타임아웃 ${TIMEOUT_SEC}초)"
  echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

  printf "$INPUT" | timeout "$TIMEOUT_SEC" dotnet run --project "$DUMMY" --no-build > "$LOG" 2>&1
  local EXIT=$?

  if [ $EXIT -eq 124 ]; then
    echo "  [경고] 타임아웃 (${TIMEOUT_SEC}초 초과)"
  else
    echo "  [완료] 종료 (exit=$EXIT)"
  fi
  cat "$LOG"
}

# ── 시나리오 1: 게임 서버 직접 접속 + CMove ─────────────────
run_scenario 1 "1\nlt_0001\n123456\nq\n0\n" 300

# ── 시나리오 2: Ticketing API (완전 자동) ────────────────────
run_scenario 2 "2\n\n0\n" 120

# ── 시나리오 3: 매칭 (두 유저 병렬) ─────────────────────────
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  [시나리오 3] 실행 중... (2개 프로세스 병렬, 타임아웃 300초)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
printf "3\nlt_0001\n123456\nm\nq\n0\n" | timeout 300 dotnet run --project "$DUMMY" --no-build > /tmp/scenario_3a.log 2>&1 &
PID3A=$!
printf "3\nlt_0002\n123456\nm\nq\n0\n" | timeout 300 dotnet run --project "$DUMMY" --no-build > /tmp/scenario_3b.log 2>&1 &
PID3B=$!
wait $PID3A $PID3B
echo "  [완료] 시나리오 3 종료"
echo "--- [lt_0001] ---"
cat /tmp/scenario_3a.log
echo "--- [lt_0002] ---"
cat /tmp/scenario_3b.log

# ── 시나리오 4: 1000명 대기열 부하 테스트 ────────────────────
run_scenario 4 "4\n\n0\n" 300

# ── 시나리오 5: 1000명 매칭 부하 테스트 ─────────────────────
run_scenario 5 "5\n\n0\n" 600

# ── 시나리오 7: 토큰 갱신 통합 테스트 ───────────────────────
run_scenario 7 "7\nlt_0001\n123456\n\n0\n" 300

# ── 시나리오 8: 중복 로그인 방어 검증 ───────────────────────
run_scenario 8 "8\nlt_0001\n123456\n\n0\n" 300
```

### 6단계: Redis 데이터 정리

```bash
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "[정리] Redis 테스트 데이터 삭제 중..."
docker exec redis-master-1 redis-cli -h 127.0.0.1 -p 6371 FLUSHALL
docker exec redis-master-1 redis-cli -h 127.0.0.1 -p 6372 FLUSHALL
docker exec redis-master-1 redis-cli -h 127.0.0.1 -p 6373 FLUSHALL
echo "[완료] Redis 3개 마스터 노드 데이터 초기화 완료"
```

### 7단계: 서버 종료

자동 시작 여부와 무관하게 4개 서버 포트를 모두 강제 종료한다.

```bash
echo ""
echo "[정리] 서버 프로세스 종료 중..."
kill_if_running "Auth.API"      7088
kill_if_running "Ticketing.API" 7075
kill_if_running "Matching.API"  7007
kill_if_running "Game.Server"   7777
sleep 2
echo "[완료] 서버 종료 완료"
```

### 8단계: 결과 리포트

다음 로그 파일들을 읽고 결과를 요약하여 출력한다:
- `/tmp/scenario_1.log`, `/tmp/scenario_2.log`
- `/tmp/scenario_3a.log`, `/tmp/scenario_3b.log`
- `/tmp/scenario_4.log`, `/tmp/scenario_5.log`
- `/tmp/scenario_7.log`, `/tmp/scenario_8.log`

각 시나리오별로 아래 정보를 추출하여 콘솔 리포트로 출력한다:

```
══════════════════════════════════════════════════════
  전체 시나리오 실행 결과
══════════════════════════════════════════════════════
  시나리오 1  [게임 서버 직접 접속]   : PASS / FAIL / TIMEOUT
  시나리오 2  [Ticketing API]         : PASS / FAIL / TIMEOUT
  시나리오 3a [매칭 lt_0001]          : PASS / FAIL / TIMEOUT
  시나리오 3b [매칭 lt_0002]          : PASS / FAIL / TIMEOUT
  시나리오 4  [1000명 대기열 부하]    : PASS / FAIL / TIMEOUT
              성공률 N%, 처리량 N명/초, 평균대기 N초
  시나리오 5  [1000명 매칭 부하]      : PASS / FAIL / TIMEOUT
              성공률 N%, P50=Nms P95=Nms P99=Nms
  시나리오 7  [토큰 갱신 통합]        : PASS / FAIL / TIMEOUT
  시나리오 8  [중복 로그인 방어]      : PASS / FAIL / TIMEOUT
══════════════════════════════════════════════════════
  전체: N/8 통과
══════════════════════════════════════════════════════
```

**PASS 판정 기준:**
- 시나리오 1, 2: 로그에 "[OK]" 또는 "성공" 키워드 존재 + exit=0
- 시나리오 3: "매칭 큐에 등록되었습니다" 키워드 존재 (MatchFound는 q 입력 타이밍에 따라 수신 안 될 수 있음)
- 시나리오 4: "성공률 N%" 라인에서 N >= 80
- 시나리오 5: "성공률 N%" 또는 "통과" 라인에서 N >= 50 (매칭은 짝이 맞아야 하므로 하한치 완화)
- 시나리오 7: "[PASS]" 키워드 존재
- 시나리오 8: "[PASS] 중복 로그인 방어" 키워드 존재
