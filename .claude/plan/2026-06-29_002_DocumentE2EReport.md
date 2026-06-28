# 요구사항 명세: DocumentE2EReport

작성일: 2026-06-29
브랜치: 2026-06-29_DocumentE2EReport
소스: .claude/plan/2026-06-29_MassGomokuE2EReport.md

## 요구사항 요약

1000명 Gomoku E2E 테스트 결과를 구조화된 JSON 리포트로 자동 출력하고, 테스트 이후 Redis/DB/Room 잔여 상태를 검증하여 플랫폼 확장 전 기준점을 확립한다.

## 상세 요구사항

### 1. JSON 리포트 자동 출력 (`MassGomokuE2EScenario.cs`)

- `PrintReport()` 메서드 끝에 JSON 직렬화 + 파일 저장 로직 추가
- 출력 경로: `reports/e2e-{yyyyMMdd-HHmmss}.json` (DummyClient 실행 디렉터리 기준)
- `TeeWriter`와 동일한 타임스탬프 사용
- JSON 필드: scenario, date, userCount, spawnRate, maxGameConcurrency, totalElapsedSeconds, loginOk/Fail, queueOk/Fail, activeOk/Fail, lobbyOk/Fail, matchReq/Ok/Timeout, tcpOk/Fail, gameStartOk, gameOverOk/Fail, verifyOk/Fail, win/lose/draw, failoverA/B/C, passed
- `passed` 필드는 E2E exit code(OVERALL PASS 여부)와 일치

### 2. `.gitignore`에 `reports/` 추가

- `logs/`와 동일한 방식으로 gitignore에 추가
- 이미 포함된 경우 스킵

### 3. E2E 실행 및 결과 수집

- `dotnet run -- --e2e 10` 실행 (시나리오 10 = MassGomokuE2E)
- Stage 10 verifyOk > 0 확인 (MATCHING_API_BASE_URL HTTPS 수정 효과)
- 생성된 JSON 파일 내용을 콘솔 출력과 비교 검증

### 4. Redis 잔여 상태 확인

E2E 종료 후 아래 키 잔여 여부 확인:
- `game_transfer:*` — 5분 TTL, 테스트 완료 후 만료 예정
- `{ticket:queue}:gomoku` (또는 global) — 정상 종료 시 빈 큐
- `player:login_lock:*` — 게임 종료 후 해제 여부

### 5. DB MatchRecord 상태 확인

```sql
SELECT Status, COUNT(*) FROM match_records GROUP BY Status;
SELECT * FROM match_records WHERE Status = 'InProgress';
```

- InProgress 잔여 없어야 정상
- Completed 레코드의 WinnerId 일치 여부 확인

### 6. 결과 문서 생성

`Docs/e2e/gomoku-mass-e2e-2026-06-29.md` 생성:
- 실행 환경 (OS, .NET 버전, 서비스 구성)
- 실행 파라미터 (userCount=1000, spawnRate=50, maxGameConcurrency=200)
- 결과 지표 (JSON 리포트 기반)
- 잔여 상태 검증 결과 (Redis/DB/Room)
- 발견된 문제 및 개선점
- 다음 우선 작업 제안

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|------|---------|
| `PlatformA/PlatformA.Game.DummyClient/Scenarios/MassGomokuE2EScenario.cs` | 수정 — JSON 리포트 출력 추가 |
| `PlatformA/PlatformA.Game.DummyClient/.gitignore` 또는 루트 `.gitignore` | 수정 — `reports/` 추가 |
| `Docs/e2e/gomoku-mass-e2e-2026-06-29.md` | 신규 생성 — 결과 문서 |

## 제약 및 주의사항

- ADR-007(Protobuf): 변경 없음
- ADR-010(Phase C DB-only): task JSON 없음, DB로만 진행
- `reports/` 디렉토리는 .gitignore에 추가하여 커밋 금지 (로그 파일과 동일 정책)
- `System.Text.Json`만 사용 (Newtonsoft 추가 금지)
- 기존 `TeeWriter` 타임스탬프 변수 재사용으로 log/json 파일명 동기화

## 구현 접근 방향

1. `MassGomokuE2EScenario.cs`의 `PrintReport()` 메서드 끝부분에 JSON 직렬화 추가
   - `System.Text.Json.JsonSerializer.Serialize(anonymous object)` 사용
   - `Directory.CreateDirectory("reports")` 후 `File.WriteAllText(path, json)` 저장
2. `.gitignore` 확인 후 `reports/` 미포함 시 추가
3. E2E 실행: `dotnet run -- --e2e 10` (서비스 전체 기동 상태에서)
4. Redis CLI로 잔여 키 패턴 검색
5. MySQL 쿼리로 MatchRecord 상태 집계
6. 결과 문서 작성

## 검증 기준

- [ ] `reports/e2e-*.json`이 E2E 실행 후 자동 생성된다
- [ ] JSON의 `passed` 필드가 OVERALL PASS/FAIL과 일치한다
- [ ] JSON 수치가 콘솔 출력과 일치한다
- [ ] `reports/` 디렉토리가 `.gitignore`에 포함되어 있다
- [ ] Stage 10 `verifyOk > 0` (HTTPS 수정 이후 개선 확인)
- [ ] Redis 잔여 키 확인 결과가 문서에 기록된다
- [ ] DB InProgress 잔여 없음이 확인된다
- [ ] `Docs/e2e/gomoku-mass-e2e-2026-06-29.md`가 생성된다

## DESIGN_REVIEW 결과

| ADR | 관련 여부 | 충돌/참고 사항 |
|-----|---------|--------------|
| ADR-001: Redis Cluster | 없음 | 잔여 키 확인은 읽기 전용 조회 |
| ADR-007: Protobuf | 없음 | 패킷 변경 없음 |
| ADR-009: PostgreSQL SDLC DB | 관련 있음 | Phase C 운영 원칙 준수 |
| ADR-010: Phase C DB-only | 관련 있음 | task JSON 없이 DB만으로 진행 |

판정: ✅ 기존 ADR 준수 — 신규 아키텍처 도입 없음, 코드 추가·문서 생성만
