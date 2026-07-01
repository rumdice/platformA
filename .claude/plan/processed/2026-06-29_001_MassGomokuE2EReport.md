# Sprint #75 — Mass Gomoku E2E 결과 문서화 및 리포트 자동화

작성일: 2026-06-29
기준 상태: PR #107 머지 이후

## 배경

PR #104~107을 통해 Gomoku 게임 서버 완성 → E2E 시나리오 구현 → E2E 자동화 CLI 추가까지 완료했다.
오늘의 목적은 신기능 추가가 아니라, 1000명 E2E 검증 결과를 프로젝트의 신뢰 가능한 기준점으로 남기는 것이다.

## 오늘의 목표

```
1000명 Gomoku E2E 테스트 결과를 문서화하고,
테스트 이후 Redis / DB / Room 잔여 상태를 검증하며,
다음 구조개선으로 나아갈 기준점을 확립한다.
```

## 작업 범위

### 포함
- MassGomokuE2EScenario에 JSON 리포트 출력 기능 추가 (`reports/e2e-{timestamp}.json`)
- E2E 실행 (시나리오 10) 및 결과 정리
- E2E 종료 후 Redis 잔여 키 확인 (game_transfer, queue, login lock)
- DB MatchRecord 잔여 상태 확인 (InProgress 미정리 여부, WinnerId 일치 여부)
- GomokuRoom cleanup 확인 (ghost room 여부)
- Failover A/B/C 이후 서버 상태 검증
- 결과 문서 생성: `Docs/e2e/gomoku-mass-e2e-2026-06-29.md`
- workreport 반영

### 제외
- Game.CarRider 신규 구현
- 대규모 Library.Game 공통화
- AI_SDLC gate-check 재활성화
- proto 파일 대규모 분리
- 대규모 인프라 변경

## 상세 요구사항

### 1. JSON 리포트 출력 (`MassGomokuE2EScenario.cs`)

E2E 종료 시점에 `reports/e2e-{yyyyMMdd-HHmmss}.json` 파일을 자동 생성한다.

```json
{
  "scenario": "MassGomokuE2E",
  "date": "2026-06-29T...",
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

- `reports/` 디렉토리는 `.gitignore`에 추가 (로그와 동일하게)
- `TeeWriter`와 동일한 타임스탬프 사용

### 2. Redis 잔여 상태 확인

E2E 종료 후 아래 키가 남아있지 않은지 확인한다:
- `game_transfer:*` — 5분 TTL, 테스트 완료 후 만료 예정
- `{ticket:queue}:gomoku` — 정상 종료 시 빈 큐
- 로그인 락 키 — 게임 종료 후 해제 여부

### 3. DB MatchRecord 상태 확인

```sql
SELECT Status, COUNT(*) FROM match_records GROUP BY Status;
SELECT * FROM match_records WHERE Status = 'InProgress';
```

- InProgress 잔여가 없어야 정상
- Completed 레코드의 WinnerId가 SGameOver.WinnerId와 일치하는지 확인

### 4. 결과 문서

`Docs/e2e/gomoku-mass-e2e-2026-06-29.md` 생성 — 실행 환경, 파라미터, 결과 지표, 잔여 상태 검증, 발견된 문제, 개선점을 포함한다.

## 구현 접근 방향

1. `MassGomokuE2EScenario.cs`의 `PrintReport()` 끝에 JSON 직렬화 + 파일 저장 추가
2. `Program.cs`에서 `.gitignore`에 `reports/` 추가 확인 (이미 있으면 스킵)
3. E2E 실행 (`dotnet run -- --e2e 10`)
4. Redis CLI로 잔여 키 확인
5. MySQL 쿼리로 MatchRecord 잔여 확인
6. 결과 문서 작성

## 검증 기준

- `reports/e2e-*.json`이 E2E 실행 후 자동 생성된다
- JSON 내용이 콘솔 출력과 일치한다
- `passed` 필드가 exit code와 일치한다
- E2E OVERALL PASS (이전 기준: 로그인 ≥90%, Active ≥85%, 게임완주 ≥70%, Failover 실행)
- Stage 10 verifyOk > 0 (이번 목표: URL 수정 효과 확인)
- 결과 문서가 `Docs/e2e/`에 생성된다
