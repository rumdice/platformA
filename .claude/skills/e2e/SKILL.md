---
name: e2e
schema_version: 1
description: E2E 시나리오(9·10·all)를 실행한다. 인프라 체크 → 전체 빌드 → 서비스 자동 기동(백그라운드) → E2E 실행 → 서비스 종료 → 결과 리포트 순서로 진행한다.
allowed-tools: Bash(docker *) Bash(dotnet *) Bash(curl *) Bash(powershell *) Bash(timeout *) Bash(python *) Bash(git *) Bash(mkdir *) Bash(ls *) Read Write
---

# E2E 시나리오 실행

## 개요

| 모드 | 명령 | 서비스 관리 | 대상 유저 |
|------|------|-----------|---------|
| 시나리오 9 | `--e2e 9` | **수동** (스킬이 기동·종료) | 2명 (Gomoku 완주) |
| 시나리오 10 | `--e2e 10` | **자동** (ServiceManager) | 1000명 + Failover |
| 전체 | `--e2e all` | **자동** (ServiceManager) | 9 → 10 순차 |

> 기본값: `10`

## 경로 설정

```bash
REPO=$(git rev-parse --show-toplevel)
SLN="$REPO/PlatformA"
DUMMY="$SLN/PlatformA.Game.DummyClient"
AUTH_DIR="$SLN/PlatformA.Auth.API"
TICKET_DIR="$SLN/PlatformA.Ticketing.API"
MATCH_DIR="$SLN/PlatformA.Matching.API"
LOBBY_DIR="$SLN/PlatformA.Game.Lobby"
GOMOKU_DIR="$SLN/PlatformA.Game.Gomoku"

SCENARIO="${ARG:-10}"   # /e2e 인수 없으면 10
```

---

## 수행 순서

### 0단계: 인수 파싱

`/e2e` 뒤에 온 텍스트로 시나리오를 결정한다.

| 입력 | SCENARIO |
|------|---------|
| `/e2e` (없음) | `10` |
| `/e2e 9` | `9` |
| `/e2e 10` | `10` |
| `/e2e all` | `all` |

---

### 1단계: 인프라 체크

Redis 클러스터와 MySQL이 실행 중인지 확인한다. 하나라도 응답 없으면 **즉시 중단**.

```bash
# Redis 컨테이너 실행 여부
docker ps --filter "name=redis-master-1" --filter "status=running" -q | grep -q . \
  || { echo "❌ Redis 클러스터(redis-master-1)가 실행 중이지 않습니다."; \
       echo "   'cd PlatformA/docker/redis-cluster && docker-compose up -d' 로 먼저 시작하세요."; exit 1; }

docker exec redis-master-1 redis-cli -h 127.0.0.1 -p 6371 PING 2>/dev/null | grep -q "PONG" \
  || { echo "❌ Redis PING 실패. 클러스터 상태를 확인하세요."; exit 1; }

echo "✅ Redis 클러스터 정상"

# MySQL 포트 응답 체크
DB_OK=$(powershell -c "try { \$c=New-Object Net.Sockets.TcpClient('localhost',3306); \$c.Close(); 'OK' } catch { 'FAIL' }" 2>/dev/null | tr -d '\r')
[ "$DB_OK" = "OK" ] || { echo "❌ MySQL(localhost:3306)가 실행 중이지 않습니다."; exit 1; }

echo "✅ MySQL 정상"
```

---

### 2단계: 전체 빌드

ServiceManager는 `--no-build`로 서비스를 실행하므로 사전 빌드가 필수다.

```bash
echo ""
echo "▶ 전체 빌드 중 (dotnet build PlatformA.sln)..."
cd "$SLN" && dotnet build PlatformA.sln -c Release -q \
  || { echo "❌ 빌드 실패. 오류를 수정하고 재실행하세요."; exit 1; }
echo "✅ 빌드 완료"
```

---

### 3단계: 시나리오 9 전용 — 서비스 수동 기동

**시나리오 10 / all 은 이 단계를 건너뛴다** — ServiceManager가 자동 처리.

시나리오 9(`Two-Player Gomoku E2E`)는 ServiceManager를 사용하지 않으므로
스킬이 직접 5개 서비스를 백그라운드로 실행하고 헬스체크를 기다린다.

```bash
if [ "$SCENARIO" = "9" ]; then

  # 포트 기반 강제 종료 헬퍼
  kill_port() {
    local PORT="$1"
    powershell -c "
      Get-NetTCPConnection -LocalPort $PORT -State Listen -ErrorAction SilentlyContinue |
      ForEach-Object { Stop-Process -Id \$_.OwningProcess -Force -ErrorAction SilentlyContinue }
    " 2>/dev/null
  }

  echo ""
  echo "▶ [시나리오 9] 기존 서비스 정리 후 재기동..."
  kill_port 7001; kill_port 7002; kill_port 7003
  kill_port 7777; kill_port 7778
  sleep 2

  # 서비스 백그라운드 실행
  (cd "$AUTH_DIR"   && dotnet run -c Release --no-build --launch-profile https > /tmp/e2e_auth.log   2>&1) &
  (cd "$TICKET_DIR" && dotnet run -c Release --no-build --launch-profile https > /tmp/e2e_ticket.log 2>&1) &
  (cd "$MATCH_DIR"  && dotnet run -c Release --no-build --launch-profile https > /tmp/e2e_match.log  2>&1) &
  (cd "$LOBBY_DIR"  && dotnet run -c Release --no-build               > /tmp/e2e_lobby.log  2>&1) &
  (cd "$GOMOKU_DIR" && dotnet run -c Release --no-build               > /tmp/e2e_gomoku.log 2>&1) &

  echo "▶ 헬스체크 대기 (최대 120초)..."

  check_http() {
    curl -k -s -o /dev/null -w "%{http_code}" --max-time 3 "$1" 2>/dev/null
  }
  check_tcp() {
    powershell -c "try { \$c=New-Object Net.Sockets.TcpClient('$1',$2); \$c.Close(); 'OK' } catch { 'FAIL' }" 2>/dev/null | tr -d '\r'
  }

  for i in $(seq 1 60); do
    A=$(check_http "https://localhost:7001/healthz")
    T=$(check_http "https://localhost:7003/healthz")
    M=$(check_http "https://localhost:7002/healthz")
    L=$(check_http "http://localhost:7777/healthz")
    G=$(check_tcp "localhost" 7778)
    READY=0
    [ "$A" != "000" ] && READY=$((READY+1))
    [ "$T" != "000" ] && READY=$((READY+1))
    [ "$M" != "000" ] && READY=$((READY+1))
    [ "$L" != "000" ] && READY=$((READY+1))
    [ "$G" = "OK" ] && READY=$((READY+1))
    if [ "$READY" -eq 5 ]; then
      echo "✅ 전체 서비스 준비 완료 (${i}*2초)"
      break
    fi
    [ $((i % 10)) -eq 0 ] && echo "  [${i}*2s] $READY/5 준비됨 (Auth=$A Ticket=$T Match=$M Lobby=$L Gomoku=$G)"
    sleep 2
  done

  if [ "$READY" -lt 5 ]; then
    echo "⚠ 일부 서비스 미준비 — 계속 진행하지만 테스트가 실패할 수 있습니다."
    echo "  Auth=$A Ticketing=$T Matching=$M Lobby=$L Gomoku=$G"
  fi
fi
```

---

### 4단계: E2E 실행

DummyClient `--e2e {N}` 모드를 실행한다.

- 시나리오 10/all: ServiceManager가 백그라운드 서비스 기동 → 테스트 → 종료를 자동 처리
- 시나리오 9: 3단계에서 이미 기동된 서비스를 사용

```bash
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "▶ E2E 시나리오 $SCENARIO 실행 시작"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# 타임아웃: 10→900초(15분), 9→180초, all→1200초(20분)
case "$SCENARIO" in
  "10")  TIMEOUT=900  ;;
  "9")   TIMEOUT=180  ;;
  "all") TIMEOUT=1200 ;;
  *)     TIMEOUT=900  ;;
esac

timeout "$TIMEOUT" dotnet run -c Release --no-build --project "$DUMMY" -- --e2e "$SCENARIO"
E2E_EXIT=$?

if [ "$E2E_EXIT" -eq 124 ]; then
  echo ""
  echo "⚠ E2E 타임아웃 (${TIMEOUT}초 초과)"
fi
```

---

### 5단계: 시나리오 9 전용 — 서비스 종료

시나리오 10/all은 ServiceManager가 이미 종료 처리했으므로 건너뛴다.

```bash
if [ "$SCENARIO" = "9" ]; then
  echo ""
  echo "▶ [시나리오 9] 백그라운드 서비스 종료 중..."
  for PORT in 7001 7002 7003 7777 7778; do
    powershell -c "
      Get-NetTCPConnection -LocalPort $PORT -State Listen -ErrorAction SilentlyContinue |
      ForEach-Object { Stop-Process -Id \$_.OwningProcess -Force -ErrorAction SilentlyContinue }
    " 2>/dev/null
  done
  sleep 1
  echo "✅ 서비스 종료 완료"
fi
```

---

### 6단계: 결과 파싱 및 리포트

DummyClient가 생성한 JSON 리포트를 읽어 핵심 지표를 출력한다.

```bash
# 최신 JSON 리포트 탐색 (DummyClient bin/.../reports/ 디렉토리)
REPORT_FILE=$(ls -t "$DUMMY"/bin/Release/*/reports/e2e-*.json 2>/dev/null | head -1)
LOG_FILE=$(ls -t "$DUMMY"/bin/Release/*/logs/e2e-*.log 2>/dev/null | head -1)

echo ""
echo "══════════════════════════════════════════════════════"
echo "  E2E 결과 — 시나리오 $SCENARIO"
echo "══════════════════════════════════════════════════════"

if [ "$E2E_EXIT" -eq 0 ]; then
  echo "  판정: ✅ PASS"
elif [ "$E2E_EXIT" -eq 124 ]; then
  echo "  판정: ⚠ TIMEOUT"
else
  echo "  판정: ❌ FAIL (exit=$E2E_EXIT)"
fi

if [ -n "$REPORT_FILE" ]; then
  echo ""
  echo "  JSON 리포트: $REPORT_FILE"
  # 핵심 지표 추출
  python3 -c "
import json, sys
with open('$REPORT_FILE') as f:
    r = json.load(f)
print(f'  로그인 성공률 : {r.get(\"loginOk\",0)}/{r.get(\"loginOk\",0)+r.get(\"loginFail\",0)} ({r.get(\"loginRate\",0):.1f}%)')
print(f'  Active 성공률 : {r.get(\"activeOk\",0)}/{r.get(\"activeOk\",0)+r.get(\"activeFail\",0)} ({r.get(\"activeRate\",0):.1f}%)')
print(f'  매칭 성공률   : {r.get(\"matchOk\",0)}/{r.get(\"matchOk\",0)+r.get(\"matchTimeout\",0)} ({r.get(\"matchRate\",0):.1f}%)')
print(f'  게임 완주율   : {r.get(\"gameOverOk\",0)}/{r.get(\"completed\",0)} ({r.get(\"gameRate\",0):.1f}%)')
print(f'  verifyOk/Fail : {r.get(\"verifyOk\",0)} / {r.get(\"verifyFail\",0)}')
" 2>/dev/null || echo "  (JSON 파싱 실패 — 로그 파일 직접 확인)"
fi

if [ -n "$LOG_FILE" ]; then
  echo "  로그 파일    : $LOG_FILE"
fi

echo "══════════════════════════════════════════════════════"
```

---

### 7단계: Docs/e2e/ 리포트 저장 (선택)

E2E 성공 또는 실패 여부와 무관하게 실행 결과를 `Docs/e2e/YYYY-MM-DD.md`에 저장한다.

JSON 리포트를 파싱하여 마크다운 리포트를 생성하고,
해당 파일이 이미 있으면 `YYYY-MM-DD_N.md`로 넘버링한다.

```bash
TODAY=$(date +%Y-%m-%d)
DOC_DIR="$REPO/Docs/e2e"
mkdir -p "$DOC_DIR"

# 파일명 충돌 시 자동 넘버링
DOC_FILE="$DOC_DIR/${TODAY}.md"
N=2
while [ -f "$DOC_FILE" ]; do
  DOC_FILE="$DOC_DIR/${TODAY}_${N}.md"
  N=$((N+1))
done

# 결과 문서 작성 (Write 도구 사용)
```

Write 도구로 아래 형식의 마크다운을 `$DOC_FILE`에 저장한다:

```markdown
# Gomoku E2E 검증 리포트 — {YYYY-MM-DD}

> 시나리오: {N} | 판정: ✅ PASS / ❌ FAIL / ⚠ TIMEOUT

## 실행 환경

| 항목 | 값 |
|------|---|
| 실행 방식 | `dotnet run -- --e2e {N}` |
| 타임아웃 | {N}초 |
| 로그 | `{LOG_FILE}` |
| JSON 리포트 | `{REPORT_FILE}` |

## 주요 지표

| 지표 | 값 |
|------|---|
| 로그인 성공률 | N% |
| Active 성공률 | N% |
| 매칭 성공률 | N% |
| 게임 완주율 | N% |
| verifyOk / verifyFail | N / N |

## 판정

{PASS / FAIL / TIMEOUT} — {1줄 설명}
```

---

### 완료 보고

```
✅ /e2e 완료

시나리오: {N}
판정    : ✅ PASS | ❌ FAIL | ⚠ TIMEOUT
리포트  : Docs/e2e/{YYYY-MM-DD}.md

주요 지표:
  로그인 성공률 : N%  (목표 ≥90%)
  Active 성공률 : N%  (목표 ≥85%)
  verifyOk     : N개
```

---

## 빠른 참고

| 명령 | 설명 |
|------|------|
| `/e2e` | 시나리오 10 (1000명 Gomoku + Failover) |
| `/e2e 9` | 시나리오 9 (2명 Gomoku 완주) |
| `/e2e 10` | 시나리오 10 (1000명 Gomoku + Failover) |
| `/e2e all` | 시나리오 9 → 10 순차 실행 |

## 서비스 포트 참고

| 서비스 | 포트 | 관리 방식 |
|--------|------|---------|
| Auth.API | 7001 | 시나리오10: ServiceManager / 시나리오9: 스킬 직접 기동 |
| Ticketing.API | 7003 | 동일 |
| Matching.API | 7002 | 동일 |
| Game.Lobby | 7777 | 동일 |
| Game.Gomoku | 7779 | 동일 |
| Redis Cluster | 6371-6376 | **사용자가 사전에 실행** (Docker) |
| MySQL | 3306 | **사용자가 사전에 실행** (Docker) |
