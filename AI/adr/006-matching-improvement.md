# ADR-006: 매칭 시스템 기능 개선

## 상태
Accepted (2026-05-11)

## 컨텍스트

기존 매칭 시스템의 문제점:

| # | 문제 | 영향 |
|---|------|------|
| 1 | Redis List 사용 — 입장 시각 추적 불가 | 타임아웃 구현 불가 |
| 2 | 매칭 취소 API 없음 | 플레이어가 대기 중 이탈 불가 |
| 3 | 무한 대기 — 타임아웃 없음 | 큐 적체, UX 최악 |
| 4 | 1초 폴링 — `Task.Delay(1000)` 고정 | 최대 1초 매칭 지연 |
| 5 | LPOP × 2 두 번의 네트워크 왕복 | 분산 환경에서 Race condition 가능 |
| 6 | `MatchingHub`에 미사용 의존성 (EngineService, GameMatchService) | 코드 가독성 저하 |

## 결정

### 자료구조 변경: Redis List → Sorted Set

`queue:gamematch:1v1`을 Redis List에서 Sorted Set으로 전환.
- `ZADD key score=now_ms member=playerId` 로 입장 시각을 score에 기록
- score 기반으로 타임아웃 식별 (`ZRANGEBYSCORE '-inf' cutoff`), 대기열 순위 조회 (`ZRANK`) 가능

### 타임아웃: 120초 초과 시 자동 제거 + MatchTimeout 이벤트

Lua 스크립트(`TIMEOUT_CLEANUP_SCRIPT`)로 원자적 범위 제거:
```lua
local timedOut = redis.call('ZRANGEBYSCORE', KEYS[1], '-inf', ARGV[1])
if #timedOut > 0 then
    redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', ARGV[1])
end
return timedOut
```
제거된 유저에게 SignalR `MatchTimeout` 이벤트 push.

### Lua 원자 2-player pop

ZPOPMIN × 2를 단일 Lua 스크립트로 원자화:
```lua
local members = redis.call('ZPOPMIN', KEYS[1], 2)
if #members < 4 then
    if #members == 2 then
        redis.call('ZADD', KEYS[1], members[2], members[1])
    end
    return {}
end
return {members[1], members[3]}
```
1명만 있을 때 pop된 유저를 원복(ZADD)하여 데이터 유실 방지.

### 폴링 단축: 1000ms → 200ms

별도 신호 체계 없이 단순 Delay 단축으로 최대 매칭 지연을 5배 개선.

### 신규 API 엔드포인트

- `DELETE /api/GameMatch/CancelMatch` — ZREM으로 대기열에서 제거
- `GET /api/GameMatch/Status` — ZRANK(순위) + ZCARD(전체 인원) 반환

### ExtractPlayerId 헬퍼 추출

기존 컨트롤러에서 반복되던 JWT 검증 코드를 `ExtractPlayerId()` private 메서드로 추출.

### MatchingHub 의존성 정리

`EngineService`와 `GameMatchService`는 MatchingHub 메서드에서 사용되지 않음.
생성자 주입을 제거하여 의도를 명확히 함.
(EngineService는 OrderController를 통해 여전히 주식 매칭에 사용됨)

## 보류 사항

- MatchRecord Status 업데이트 (InProgress → Completed): Game.Server HTTP 콜백 구현 필요 — 결합도 증가 우려로 BACKLOG 등록
- GameRoom ACK 보장: SignalR push 전 Game.Server에서 방 생성 완료 확인 — 복잡도 대비 발생 빈도 낮음

## 결과

- 플레이어가 매칭 대기 중 취소 가능
- 2분 초과 대기 시 자동 퇴장 및 클라이언트 알림
- 분산 환경에서 Race condition 없는 매칭 팝
- 최대 매칭 지연 1초 → 200ms
