# 요구사항 명세: RemoveMatchingDirectAccess

작성일: 2026-07-09
브랜치: 2026-07-09_RemoveMatchingDirectAccess
소스: 직접 입력 (워크플로 인수)

## 요구사항 요약
Game.Lobby 도입 이전 설계의 잔재인 클라이언트→Matching.API 직접 호출 경로를 전부 제거한다.
상태 변경(신청·취소·상태)은 Lobby 경유, 읽기 전용(history·rating)은 직접 유지라는 명확한 원칙을 코드에 반영한다.

## 상세 요구사항

### 1. Matching.API — MatchingHub 제거
- `Hubs/MatchingHub.cs` 파일 삭제
- `Program.cs`의 `app.MapHub<MatchingHub>("/hubs/matching")` 제거
- SignalR이 Matching.API에서 더 이상 필요 없으면 `AddSignalR()` 등록도 제거

### 2. Matching.API — 구 클라이언트 엔드포인트 제거
- `POST /api/GameMatch/RequestMatch` 제거 ([Deprecated] 명시된 구 단일 큐 엔드포인트)
- `DELETE /api/GameMatch/CancelMatch` (JWT 인증 버전) 제거
- `GET /api/GameMatch/Status` (JWT 인증 버전) 제거
- 관련 `[Deprecated]` 서비스 메서드(`AddPlayerToQueueAsync`, `RemovePlayerFromQueueAsync`, `GetQueueRankAsync`, `GetQueueLengthAsync`) 제거

### 3. Matching.API — 내부 전용 엔드포인트 추가
- `POST /api/GameMatch/cancel` — JWT 없음, body: `{userId, gameType}`
  - Lobby의 CancelMatch/OnDisconnected가 호출하는 전용 엔드포인트
- `GET /api/GameMatch/status/{userId}` — JWT 없음, userId 경로 파라미터
  - Lobby의 GetMatchStatus가 호출하는 전용 엔드포인트
- 기존 `/request` 엔드포인트와 동일한 내부 전용 패턴 적용

### 4. LobbyHub — JWT 포워딩 제거
- `OnDisconnectedAsync`: JWT 포워딩 → 새 `POST /api/GameMatch/cancel` 호출
- `CancelMatch()` 메서드: JWT 포워딩 → 새 `POST /api/GameMatch/cancel` 호출
- `GetMatchStatus()` 메서드: JWT 포워딩 → 새 `GET /api/GameMatch/status/{userId}` 호출
- userId는 이미 Lobby가 SignalR Context에서 보유 중이므로 JWT 불필요

### 5. DummyClient — 구 시나리오 정리
- `LoadTestMatchingScenario.cs` 삭제 (Scenario 10 MassGomokuE2EScenario가 완전 대체)
- `MatchingScenario.cs` 재작성: MATCH_HUB_URL + MATCH_API_URL → LOBBY_HUB_URL 경유
  - Lobby SignalR `RequestMatch(gameType)` 호출
  - Lobby SignalR `MatchFound` 이벤트 수신
  - TCP 로비(1번방) 접속 코드 제거 (구 Game.Server 방식)
- `Program.cs`에서 Scenario 5 (LoadTestMatchingScenario) 진입점 제거

### 6. Tests — GameMatchControllerTests 정리
- `RequestMatch_*` 테스트 제거 (엔드포인트 삭제)
- `CancelMatch_*` 테스트 제거 (JWT 버전 엔드포인트 삭제)
- `GetStatus_*` 테스트 제거 (JWT 버전 엔드포인트 삭제)
- 새 내부 엔드포인트(`cancel`, `status/{userId}`) 테스트 추가
- 유지되는 `request`, `start`, `result`, `history`, `rating` 테스트는 보존

### 7. Consts.cs — 미사용 상수 제거
- `MATCH_API_URL` 제거
- `MATCH_HUB_URL` 제거

## 영향 범위 (예상)

| 파일 | 변경 종류 |
|------|---------|
| `PlatformA.Matching.API/Hubs/MatchingHub.cs` | 삭제 |
| `PlatformA.Matching.API/Program.cs` | MapHub 제거, AddSignalR 제거 가능성 |
| `PlatformA.Matching.API/Controllers/GameMatchController.cs` | 3개 엔드포인트 제거, 2개 추가 |
| `PlatformA.Matching.API/Services/GameMatchService.cs` | Deprecated 메서드 4개 제거 |
| `PlatformA.Game.Lobby/Hubs/LobbyHub.cs` | JWT 포워딩 → 내부 엔드포인트 호출 |
| `PlatformA.Game.DummyClient/Scenarios/MatchingScenario.cs` | 재작성 |
| `PlatformA.Game.DummyClient/Scenarios/LoadTestMatchingScenario.cs` | 삭제 |
| `PlatformA.Game.DummyClient/Program.cs` | Scenario 5 진입점 제거 |
| `PlatformA.Tests.Matching.API/GameMatchControllerTests.cs` | 구 테스트 제거, 신규 추가 |
| `PlatformA.Library/Common/Consts.cs` | 상수 2개 제거 |

## 제약 및 주의사항
- ADR-011 (Lobby = 클라이언트 SignalR 허브) 준수 — 이번 작업이 이를 코드 수준에서 완성
- ADR-006 (MatchingHub 미사용 의존성 문제 기인) 준수
- 유지 대상: `GET /history`, `GET /rating/{userId}` — 읽기 전용, 상태 없음
- 유지 대상: `POST /request`, `POST /start`, `POST /result` — 내부 서비스 호출용
- Lobby의 내부 HTTP 클라이언트가 Matching API 베이스 URL(`MATCHING_API_BASE_URL`)을 사용하는지 확인 필요

## 구현 접근 방향
1. Matching.API 먼저 수정 (새 내부 엔드포인트 추가 후 구 엔드포인트 제거)
2. LobbyHub 수정 (새 엔드포인트 호출로 교체)
3. DummyClient 정리 (삭제 후 재작성)
4. 테스트 동기화
5. Consts 정리

## 검증 기준
- `dotnet build PlatformA.sln` 오류 없음
- `dotnet test PlatformA.sln` 전체 통과
- `GET /api/GameMatch/RequestMatch`, `DELETE /CancelMatch`(JWT), `GET /Status`(JWT) 엔드포인트 미존재 확인
- `POST /api/GameMatch/cancel`, `GET /api/GameMatch/status/{userId}` 엔드포인트 동작 확인
- LobbyHub `CancelMatch()` 호출 시 JWT 헤더 포워딩 없이 내부 엔드포인트 호출 확인
- `MATCH_API_URL`, `MATCH_HUB_URL` 상수 미사용 확인
