# 요구사항 명세: LobbySignalRRedesign

작성일: 2026-06-23
브랜치: 2026-06-23_LobbySignalRRedesign
소스: 직접 입력 (workflow 인수)

## 요구사항 요약

Game.Lobby를 TCP + Protobuf 구조에서 ASP.NET Core + SignalR 기반으로 전면 전환한다.
클라이언트는 JWT로 Lobby에 SignalR 연결을 맺고 매칭 신청을 하며, 매칭 성사 시 게임 서버 정보를 수신한다.

## 상세 요구사항

### 1. Game.Lobby csproj 재구성
- TCP 소켓 관련 의존 제거
- 추가 패키지: `Microsoft.AspNetCore.SignalR`, JWT 관련 패키지 (이미 Library에 있으면 재사용)
- ASP.NET Core Web App으로 전환 (SDK: `Microsoft.NET.Sdk.Web`)

### 2. Program.cs 전면 재작성
- `WebApplication.CreateBuilder()` 기반
- `AddSignalR()`, `AddAuthentication().AddJwtBearer()` 등록
- `AddHttpClient("MatchingAPI")` 등록
- `MapHub<LobbyHub>("/hubs/lobby")` 매핑
- Redis 초기화 (기존 RedisManager 재사용)
- `/healthz`, `/readyz` 헬스체크 등록

### 3. Hubs/LobbyHub.cs 생성
- `OnConnectedAsync`: JWT 검증 (QueryString `?access_token=` 또는 Authorization 헤더), 실패 시 `Context.Abort()`
  - `TokenManager.ValidateTokenAndGetUserId()` 재사용
  - `LobbyPresenceService`에 등록
- `OnDisconnectedAsync`: 유저 제거, 매칭 대기 중이면 자동 취소
- `RequestMatch(string gameType)`: Matching.API `POST /api/gamematch/request` 호출
  - 즉시 매칭: 클라이언트에 `MatchFound` push
  - 대기: 클라이언트에 `MatchQueued` push
- `CancelMatch(string gameType)`: Matching.API `DELETE /api/gamematch/CancelMatch` 호출 (JWT forwarding)
- `GetStatus(string gameType)`: 대기열 순위 조회

### 4. Services/LobbyPresenceService.cs 생성 (싱글톤)
- `ConcurrentDictionary<int userId, string connectionId>` 로 온라인 유저 추적
- `Register(int userId, string connectionId)`, `Unregister(int userId)`, `GetConnectionId(int userId)` 메서드

### 5. Services/MatchNotificationService.cs 생성 (BackgroundService)
- Redis `channel:match_found` 구독
- 메시지 포맷: `{ "userId": int, "host": string, "port": int, "roomId": string }`
- 수신 시 `IHubContext<LobbyHub>`로 해당 유저에게 `MatchFound` 전송

### 6. 구 TCP 코드 제거
- `Network/GameSession.cs` 삭제
- `Packet/PacketHandler.cs` 삭제
- `Network/LobbyHttpClientFactory.cs` 삭제
- TCP 관련 import/namespace 정리

### 7. Matching.API TryMatchAsync 수정
- 매칭 성사 시 두 플레이어 모두 `channel:match_found`에 Redis publish
- 메시지: `{ "userId": int, "host": string, "port": int, "roomId": string, "gameType": string }`
- 요청자(player B)는 HTTP 응답 + Redis publish 둘 다 처리
- 대기자(player A)는 Redis publish만 (HTTP 연결 없음)

### 8. PlatformA.Game.Server 삭제
- `PlatformA.sln`에서 프로젝트 참조 제거
- `PlatformA/PlatformA.Game.Server/` 디렉토리 삭제
- `PlatformA/PlatformA.Tests.Game.Server/` 디렉토리도 확인 (이미 Lobby로 리네임됐으면 skip)

### 9. Tests.Game.Lobby 업데이트
- 기존 TCP 패킷 테스트 제거 (PacketFramingTests, LoginPacketTests, MovePacketTests, EnterRoomPacketTests)
- LobbyHub 통합 테스트 추가 (WebApplicationFactory 패턴):
  - `RequestMatch_ValidToken_Returns202OrOk`
  - `OnConnected_InvalidToken_Aborts`
  - `CancelMatch_ValidToken_Returns200`
- GameRoomManagerTests, JobQueueTests, SessionManagerTests — Library.Game 기반이므로 유지

## 영향 범위 (예상)

| 파일/경로 | 변경 유형 | 위험도 |
|---------|---------|--------|
| `PlatformA.Game.Lobby/Game.Lobby.csproj` | 전면 수정 | 🔴 HIGH |
| `PlatformA.Game.Lobby/Program.cs` | 전면 재작성 | 🔴 HIGH |
| `PlatformA.Game.Lobby/Hubs/LobbyHub.cs` | 신규 생성 | 🔴 HIGH |
| `PlatformA.Game.Lobby/Services/LobbyPresenceService.cs` | 신규 생성 | 🟡 MEDIUM |
| `PlatformA.Game.Lobby/Services/MatchNotificationService.cs` | 신규 생성 | 🟡 MEDIUM |
| `PlatformA.Game.Lobby/Network/GameSession.cs` | 삭제 | 🟡 MEDIUM |
| `PlatformA.Game.Lobby/Packet/PacketHandler.cs` | 삭제 | 🟡 MEDIUM |
| `PlatformA.Matching.API/Services/GameMatchService.cs` | TryMatchAsync 수정 | 🔴 HIGH |
| `PlatformA.Library/Common/Consts.cs` | MATCH_FOUND_CHANNEL 상수 추가 | 🟢 LOW |
| `PlatformA.sln` | Game.Server 제거 | 🟡 MEDIUM |
| `PlatformA/PlatformA.Game.Server/` | 디렉토리 삭제 | 🟡 MEDIUM |
| `PlatformA.Tests.Game.Lobby/` | 테스트 교체 | 🟡 MEDIUM |
| `PlatformA.Tests.Matching.API/` | TryMatchAsync publish 테스트 보완 | 🟢 LOW |

## 제약 및 주의사항

- ADR-011: Game.Lobby는 SignalR, 순수 게임 서버는 TCP + Protobuf (ADR-007 범위 명확화)
- Redis 채널명 `channel:match_found`는 `Consts.cs`에 상수로 등록
- JWT 검증은 기존 `TokenManager.ValidateTokenAndGetUserId()` 재사용 — 새 라이브러리 도입 금지
- SignalR JWT: QueryString `?access_token=` 방식 (브라우저/모바일 표준 패턴)
- Matching.API에는 `AddAuthentication`/`AddJwtBearer`가 없으므로 `[Authorize]` 사용 금지 (기존과 동일)
- Game.Server 삭제 시 Tests.Game.Server가 이미 Tests.Game.Lobby로 리네임됐는지 확인 후 삭제

## 구현 접근 방향

1. **csproj 수정** → SDK를 `Microsoft.NET.Sdk.Web`으로, 패키지 추가
2. **Consts.cs** → `MATCH_FOUND_CHANNEL` 상수 추가
3. **LobbyPresenceService** → 싱글톤 서비스, Hub보다 먼저 등록
4. **MatchNotificationService** → BackgroundService, RedisManager.GetSubscriber().Subscribe()
5. **LobbyHub** → OnConnectedAsync에서 JWT 검증 + PresenceService 등록
6. **Program.cs** → 순서: 서비스 등록 → 미들웨어 → Hub 매핑
7. **Game.Server 삭제** → sln 편집 후 디렉토리 삭제
8. **Matching.API** → TryMatchAsync에 두 플레이어 publish 추가
9. **Tests** → 기존 TCP 테스트 제거, Hub 테스트 추가

## 검증 기준

- `dotnet build PlatformA.sln` 빌드 오류 0개
- `dotnet test PlatformA.sln` 전체 통과
- `PlatformA.Game.Server` 디렉토리 및 sln 참조가 존재하지 않음
- LobbyHub 테스트: 유효한 JWT로 연결 시 200, 잘못된 JWT로 연결 시 연결 중단
- MatchNotificationService 테스트: Redis 메시지 수신 시 해당 유저에게 SignalR push 호출 확인
- Matching.API TryMatchAsync: 매칭 성사 시 Redis publish 2회 (양쪽 플레이어)
