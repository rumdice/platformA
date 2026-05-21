# Plan: run-scenarios 스킬 작성

## Context
DummyClient 시나리오 테스트가 코드로 작성되어 있지만, 실제 실행을 자동화하는 방법이 없었다.
사용자가 `/run-scenarios` 스킬을 요청했다: 시나리오 6(미구현)을 제외한 1~5, 7, 8번 모두를
인프라 체크 → 서버 자동 시작 → stdin 파이핑으로 자동 실행 → Redis 정리 → 서버 종료 순서로
처리하고 콘솔 결과 리포트를 출력한다.

---

## 파일 생성 위치
`.claude/skills/run-scenarios/SKILL.md`

---

## 수집한 핵심 정보

### 포트 및 엔드포인트 (Consts.cs 기준)
| 서버 | URL | 프로토콜 |
|---|---|---|
| Auth.API | https://localhost:7088 | HTTPS |
| Ticketing.API | https://localhost:7075 | HTTPS |
| Matching.API | http://localhost:5189 | HTTP |
| Game.Server | 127.0.0.1:7777 | TCP |

### Redis Docker 컨테이너
- `redis-master-1` (6371), `redis-master-2` (6372), `redis-master-3` (6373)
- CLI 사용: `docker exec redis-master-1 redis-cli -h 127.0.0.1 -p 6371 PING`

### 각 시나리오 stdin 패턴 (한 프로세스 당)
| 시나리오 | 설명 | stdin 패턴 |
|---|---|---|
| 1 | 직접 TCP + CMove 인터랙티브 | `1\nlt_0001\n123456\nq\n0\n` |
| 2 | TicketingScenario (자동) | `2\n\n0\n` |
| 3 | MatchingScenario (병렬 2개) | `3\nlt_0001\n123456\nm\nq\n0\n` + `3\nlt_0002\n123456\nm\nq\n0\n` |
| 4 | LoginWait 1000명 대기열 부하 (자동) | `4\n\n0\n` |
| 5 | LoadTestMatching 1000명 매칭 부하 (자동) | `5\n\n0\n` |
| 7 | 토큰 갱신 통합 테스트 (반자동) | `7\nlt_0001\n123456\n\n0\n` |
| 8 | 중복 로그인 방어 검증 (반자동) | `8\nlt_0001\n123456\n\n0\n` |

### 시나리오 실행 특성
- **Scenario 1**: 대기열 대기 후 CMove 루프 진입. `q`를 파이프하면 루프 종료. 타임아웃: 5분
- **Scenario 2**: 완전 자동. 자체 랜덤 계정 사용. 타임아웃: 2분
- **Scenario 3**: 두 프로세스 병렬 실행 → `wait` 동기화. 타임아웃: 5분
- **Scenario 4**: 완전 자동, 1000명, ~60-120초. 타임아웃: 5분
- **Scenario 5**: 완전 자동, 1000명+매칭, ~130-300초. 타임아웃: 10분
- **Scenario 7**: lt_0001 계정으로 자동. 타임아웃: 5분
- **Scenario 8**: lt_0001 계정으로 자동 (두 TCP 동시 연결). 타임아웃: 5분

### 프로젝트 경로 (솔루션 루트 기준)
```
SLN=$(git rev-parse --show-toplevel)/PlatformA
DUMMY="$SLN/PlatformA.Game.DummyClient"
AUTH="$SLN/PlatformA.Auth.API"
TICKET="$SLN/PlatformA.Ticketing.API"
MATCH="$SLN/PlatformA.Matching.API"
GAME="$SLN/PlatformA.Game.Server"
```

---

## 스킬 구현 순서

### 1단계: 인프라 체크 (실패 시 즉시 중단)
```bash
# Redis 클러스터 체크
docker ps --filter "name=redis-master-1" --filter "status=running" -q | grep -q . \
  || { echo "[오류] Redis 클러스터(redis-master-1)가 실행 중이지 않습니다."; exit 1; }
docker exec redis-master-1 redis-cli -h 127.0.0.1 -p 6371 PING 2>/dev/null | grep -q "PONG" \
  || { echo "[오류] Redis에 PING이 실패했습니다."; exit 1; }

# MariaDB 체크 (포트 3306 응답 여부)
powershell -c "try { \$c=New-Object Net.Sockets.TcpClient('localhost',3306); \$c.Close(); 'OK' } catch { 'FAIL' }" \
  | grep -q "OK" || { echo "[오류] MariaDB(3306)가 실행 중이지 않습니다."; exit 1; }
```

### 2단계: .NET 서버 자동 시작
서버마다: HTTP/TCP 포트 응답 체크 → 미응답 시 배경 실행 + health poll

```bash
# 예시: Auth.API 체크 + 시작
AUTH_CODE=$(curl -k -s -o /dev/null -w "%{http_code}" --max-time 2 "https://localhost:7088/" 2>/dev/null)
if [ "$AUTH_CODE" = "000" ]; then
  echo "[시작] Auth.API 실행 중..."
  cd "$AUTH" && dotnet run > /tmp/auth_api.log 2>&1 &
  AUTH_PID=$!
  STARTED_PIDS="$STARTED_PIDS $AUTH_PID"
  # health poll: 최대 30초 대기
  for i in $(seq 1 15); do
    sleep 2
    CODE=$(curl -k -s -o /dev/null -w "%{http_code}" --max-time 2 "https://localhost:7088/" 2>/dev/null)
    [ "$CODE" != "000" ] && break
  done
else
  echo "[확인] Auth.API 이미 실행 중"
fi
```
동일 패턴을 Ticketing.API (7075), Matching.API (5189), Game.Server TCP (7777)에 적용.
Game.Server는 TCP 포트이므로 PowerShell `TcpClient` 방식으로 체크.

### 3단계: DummyClient 사전 빌드
```bash
cd "$SLN" && dotnet build PlatformA.Game.DummyClient/PlatformA.Game.DummyClient.csproj -q
```

### 4단계: 각 시나리오 실행 (결과 로그 캡처)
```bash
run_scenario() {
  local NUM="$1" INPUT="$2" TIMEOUT_SEC="$3"
  echo ""
  echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  echo "  [시나리오 $NUM] 실행 중... (타임아웃 ${TIMEOUT_SEC}초)"
  echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  local LOG="/tmp/scenario_${NUM}.log"
  printf "$INPUT" | timeout "$TIMEOUT_SEC" dotnet run --project "$DUMMY" --no-build > "$LOG" 2>&1
  local EXIT=$?
  if [ $EXIT -eq 124 ]; then
    echo "  [경고] 시나리오 $NUM 타임아웃 (${TIMEOUT_SEC}초 초과)"
  else
    echo "  [완료] 시나리오 $NUM 종료 (exit=$EXIT)"
  fi
  cat "$LOG"
}

# 시나리오 순서 실행
run_scenario 1 "1\nlt_0001\n123456\nq\n0\n" 300
run_scenario 2 "2\n\n0\n" 120
# 시나리오 3: 병렬
printf "3\nlt_0001\n123456\nm\nq\n0\n" | timeout 300 dotnet run --project "$DUMMY" --no-build > /tmp/scenario_3a.log 2>&1 &
PID3A=$!
printf "3\nlt_0002\n123456\nm\nq\n0\n" | timeout 300 dotnet run --project "$DUMMY" --no-build > /tmp/scenario_3b.log 2>&1 &
PID3B=$!
wait $PID3A $PID3B
cat /tmp/scenario_3a.log /tmp/scenario_3b.log
run_scenario 4 "4\n\n0\n" 300
run_scenario 5 "5\n\n0\n" 600
run_scenario 7 "7\nlt_0001\n123456\n\n0\n" 300
run_scenario 8 "8\nlt_0001\n123456\n\n0\n" 300
```

### 5단계: Redis 데이터 정리
```bash
echo ""
echo "[정리] Redis 테스트 데이터 삭제 중..."
# 각 마스터 노드 FLUSHALL
docker exec redis-master-1 redis-cli -h 127.0.0.1 -p 6371 FLUSHALL
docker exec redis-master-1 redis-cli -h 127.0.0.1 -p 6372 FLUSHALL
docker exec redis-master-1 redis-cli -h 127.0.0.1 -p 6373 FLUSHALL
echo "[완료] Redis 데이터 초기화 완료"
```

### 6단계: 자동 시작한 서버 종료
```bash
if [ -n "$STARTED_PIDS" ]; then
  echo "[정리] 자동 시작된 서버 프로세스 종료 중... (PIDs: $STARTED_PIDS)"
  kill $STARTED_PIDS 2>/dev/null
  echo "[완료] 서버 종료 완료"
fi
```

### 7단계: 결과 리포트
Claude가 각 시나리오 로그(`/tmp/scenario_N.log`)를 읽어 결과를 요약 출력:
- 각 시나리오별 PASS/FAIL/TIMEOUT 상태
- 시나리오 4, 5의 성능 지표 (성공률, 처리량, P50/P95/P99) 추출
- 전체 통과 여부 판정

---

## 검증 방법
1. `dotnet build PlatformA.sln -q` — 빌드 오류 없음 확인
2. Redis + MariaDB + 서버 실행 상태에서 `/run-scenarios` 호출
3. 각 시나리오 로그 파일 확인 (PASS 키워드, 성능 지표)
4. Redis FLUSHALL 후 키 없음 확인: `docker exec redis-master-1 redis-cli -h 127.0.0.1 -p 6371 DBSIZE`
