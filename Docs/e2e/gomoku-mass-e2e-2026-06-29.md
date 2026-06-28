# Gomoku Mass E2E 검증 리포트 — 2026-06-29

> 기준 커밋: PR #107 (Sprint #74) 머지 이후 + 버그 수정(Consts.cs HTTPS, GomokuRoom SSL)

---

## 실행 환경

| 항목 | 값 |
|------|---|
| OS | Windows 11 Home 10.0.26200 |
| .NET | 8.0 (서버), 9.0 (Matching.API) |
| 실행 방식 | `dotnet run -- --e2e 10` |
| 로그 | `logs/e2e-{timestamp}.log` (TeeWriter) |
| JSON 리포트 | `reports/e2e-{timestamp}.json` (Sprint #75 추가) |

### 기동 서비스 구성

| 서비스 | 포트 | 프로토콜 |
|--------|------|---------|
| Auth.API | :7001 | HTTPS |
| Ticketing.API | :7003 | HTTPS |
| Matching.API | :7002 | HTTPS |
| Game.Gomoku | :7778 | TCP |
| Game.Lobby | :7777 | HTTP/WebSocket |
| Redis Cluster | 6371-6376 | TCP |
| MySQL | 3306 | TCP |

---

## 실행 파라미터

```
userCount           = 1000
spawnRate           = 50명/초
maxGameConcurrency  = 200
matchTimeoutSec     = 60
gameTimeoutSec      = 90
totalTimeoutSec     = 600
failoverA(orphan)   = 5%  (50명)
failoverB(ghost)    = 5%  (50명)
failoverC(abandon)  = 5%  (50명)
```

---

## E2E 결과 (2026-06-25 기준 실행 — HTTPS 수정 前)

> PR #107 머지 직후 실행. MATCHING_API_BASE_URL이 `http://localhost:7002` 기본값이었던 상태.

| 단계 | 지표 | 값 | 기준 | 판정 |
|------|------|---|------|------|
| Stage 1: Auth 로그인 | 성공/실패 | 919 / 81 | ≥90% | ✅ 91.9% |
| Stage 2: 대기열 진입 | 성공/실패 | 919 / 0 | — | ✅ |
| Stage 3: Active 대기 | 성공/실패 | 919 / 0 | ≥85% | ✅ 91.9% |
| Failover-A(orphan) | 실행 수 | 46명 | 목표 50 | ✅ |
| Stage 4: Lobby SignalR | 성공/실패 | 873 / 0 | — | ✅ |
| Stage 5+6: RequestMatch→MatchFound | 요청/성사/타임아웃 | 873 / 864 / 9 | — | ✅ |
| Failover-B(ghost) | 실행 수 | 46명 | 목표 50 | ✅ |
| Stage 7+8: TCP→SGameStart | TCP성공/실패, GameStart | 818✔ 0✗ / 776 | — | ✅ |
| Failover-C(abandon) | 실행 수 | 42명 | 목표 50 | ✅ |
| Stage 9: 게임 완주 | 완주/실패 | 734 / 116 | ≥70% | ✅ 86.4% |
| Stage 10: MatchRecord 검증 | 성공/실패 | **0 / 734** | — | ❌ (버그) |

**결과 분포**: 승 388 / 패 346 / 무 0  
**총 소요 시간**: 158.1초  
**OVERALL**: ✅ PASS (Stage 10은 PASS 판정에 미포함)

### Stage 10 실패 원인 (Root Cause)

| # | 버그 | 위치 | 내용 |
|---|------|------|------|
| D | MATCHING_API_BASE_URL 기본값 HTTP | `Consts.cs` | `http://localhost:7002` → Kestrel HTTPS 전용 포트에 400 응답 |
| E | GomokuRoom._httpClient SSL 미설정 | `GomokuRoom.cs` | 자체 서명 인증서 검증 실패 → `/api/gamematch/result` 무음 실패 |

**수정 완료**: Sprint #75 이전에 main 커밋으로 반영됨.

```csharp
// Consts.cs — HTTPS로 수정
public static readonly string MATCHING_API_BASE_URL =
    Environment.GetEnvironmentVariable("MATCHING_API_BASE_URL")
    ?? "https://localhost:7002";

// GomokuRoom.cs — SSL bypass 핸들러 추가
private static readonly HttpClient _httpClient = new HttpClient(
    new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
    }) { BaseAddress = new Uri(Consts.MATCHING_API_BASE_URL), ... };
```

---

## Sprint #75 코드 변경 사항

### 1. JSON 리포트 자동 출력 (`MassGomokuE2EScenario.cs`)

`PrintReport()` 종료 시점에 `SaveJsonReport()`를 호출하여 아래 경로에 JSON 파일을 자동 생성한다:

```
reports/e2e-{yyyyMMdd-HHmmss}.json
```

출력 예시:
```json
{
  "scenario": "MassGomokuE2E",
  "date": "2026-06-25T...",
  "userCount": 1000,
  "spawnRate": 50,
  "maxGameConcurrency": 200,
  "totalElapsedSeconds": 158.1,
  "loginOk": 919, "loginFail": 81,
  "queueOk": 919, "queueFail": 0,
  "activeOk": 919, "activeFail": 0,
  "lobbyOk": 873, "lobbyFail": 0,
  "matchReq": 873, "matchOk": 864, "matchTimeout": 9,
  "tcpOk": 818, "tcpFail": 0,
  "gameStartOk": 776,
  "gameOverOk": 734, "gameOverFail": 116,
  "verifyOk": 0, "verifyFail": 734,
  "win": 388, "lose": 346, "draw": 0,
  "failoverA": 46, "failoverB": 46, "failoverC": 42,
  "passed": true
}
```

### 2. `.gitignore` 추가

```
**/logs/
**/reports/
```

`reports/` 디렉터리는 로컬 생성물이며 git 추적 대상 아님.

---

## 잔여 상태 검증 항목

> E2E 재실행 후 아래 항목을 수동으로 검증한다.

### Redis 잔여 키 확인

```bash
# Redis Cluster 접속 (노드 중 하나)
redis-cli -p 6371

# 확인 대상 키 패턴
KEYS game_transfer:*        # 5분 TTL — 완료 후 자동 만료 예정
ZRANGE {ticket:queue}:global 0 -1  # 정상 종료 시 빈 큐
KEYS player:login_lock:*    # 게임 종료 후 해제
```

| 키 패턴 | 예상 상태 | 설명 |
|---------|---------|------|
| `game_transfer:*` | 만료됨(TTL 5분) | E2E 완료 5분 후 자동 소멸 |
| `{ticket:queue}:global` | 비어 있음 | 정상 종료 후 큐 비어야 함 |
| `player:login_lock:*` | 해제됨 | TCP 연결 종료 시 자동 해제 |

### DB MatchRecord 상태 확인

```sql
-- MySQL 연결: db_WebApp
SELECT Status, COUNT(*) as cnt FROM match_records GROUP BY Status;
SELECT * FROM match_records WHERE Status = 'InProgress';
SELECT mr.WinnerId, mr.Status, mr.UpdatedAt 
FROM match_records mr 
WHERE Status = 'Completed' 
ORDER BY UpdatedAt DESC 
LIMIT 20;
```

| 조건 | 예상 결과 |
|------|---------|
| `InProgress` 잔여 | 0건 (모두 Completed 또는 Aborted) |
| `Completed` 레코드 수 | ≈ gameOverOk 수 |
| WinnerId 일치 | SGameOver.WinnerId와 match_records.WinnerId 동일 |

---

## 차기 E2E 실행 기대 결과

HTTPS 수정(버그 D+E)이 반영된 이후 실행 시:

| 항목 | 이전 (06-25) | 기대 (수정 後) |
|------|-------------|--------------|
| Stage 10 verifyOk | 0 | > 0 (≈ gameOverOk 수) |
| GomokuRoom 결과 보고 | 무음 실패 | HTTP 200 정상 응답 |
| JSON 리포트 | 미생성 | `reports/e2e-*.json` 자동 생성 |

---

## 발견된 문제 및 개선점

### 1. fire-and-forget 실패 가시성

**문제**: `GomokuRoom.ReportMatchResultAsync()`가 실패해도 `Console.WriteLine`만 출력 → 운영 환경에서 감지 불가  
**개선 제안**: `LogWarning` 레벨로 올리고 실패율을 Prometheus 메트릭으로 집계

### 2. E2E 잔여 계정 누적

**문제**: `mass_e2e_{1..1000}` 계정이 MySQL에 영구 잔존  
**개선 제안**: E2E 완료 후 자동 정리 스크립트 또는 별도 테스트 DB 격리

### 3. Failover 목표 미달 (A: 46/50, B: 46/50, C: 42/50)

**원인**: 해시 기반 결정론적 분배지만 실제 유저 생성 수(919명 로그인)가 1000명 미달  
**영향**: PASS 기준 통과이나 목표 대비 약 10% 미달  
**개선 제안**: Failover 판정 기준을 "절대값 목표" 대신 "실제 정상 유저 비율"로 전환

---

## 다음 우선 작업 제안

1. **E2E 재실행** — Sprint #75 코드(JSON 리포트) 반영 후 `dotnet run -- --e2e 10` 실행, Stage 10 verifyOk > 0 확인
2. **Game.Gomoku /readyz 추가** — Redis + Matching.API 헬스체크 엔드포인트 (현재 없음)
3. **ILogger 전환** — GomokuRoom의 `Console.WriteLine` → `ILogger<GomokuRoom>` (운영 로깅 요건)
4. **MMR 업데이트 연동** — Matching.API `match_records`에서 ELO 점수 업데이트 미연동 상태
5. **E2E 계정 정리 스크립트** — `mass_e2e_*` 계정 일괄 삭제 CLI 추가
