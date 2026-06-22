# 요구사항 명세: GameLobbyAndMatchingUpgrade

작성일: 2026-06-22
브랜치: 2026-06-22_GameLobbyAndMatchingUpgrade
소스: 대화 세션 — 게임 서버 아키텍처 설계 논의

## 요구사항 요약

Game.Server를 Lobby 서버(모든 클라이언트의 TCP 진입점, 로그인/매칭 요청/라우팅 전담)로 역할을 재정의하고, Matching.API에 MMR 기반 매칭 알고리즘과 match_records 저장을 추가하여 매칭→게임 진입 E2E 플로우를 완성한다.

## 상세 요구사항

### 1. PlatformA.Game.Server → PlatformA.Game.Lobby 전환

**1-1. 프로젝트 리네임**
- `PlatformA/PlatformA.Game.Server/` → `PlatformA/PlatformA.Game.Lobby/`
- `.csproj` 파일명: `PlatformA.Game.Lobby.csproj`
- 최상위 namespace: `PlatformA.Game.Lobby`
- `PlatformA.sln`에서 프로젝트 참조 경로 업데이트
- `Dockerfile` 경로 및 내용 업데이트

**1-2. 새 패킷 추가 (packets.proto)**
```protobuf
message CMatchRequest {
  string game_type = 1;  // "gomoku", "cartrider" 등
}

message SMatchFound {
  string host    = 1;
  int32  port    = 2;
  string room_id = 3;
}
```
- `Packet.oneof`에 `CMatchRequest`, `SMatchFound` 필드 등록
- `PacketManager<LobbySession>`에 핸들러 자동 등록

**1-3. CMatchRequest 핸들러 구현**
흐름:
```
CMatchRequest{gameType}
  → Matching.API POST /api/matching/request {userId, gameType} (HTTP)
  → 매칭 완료 응답 수신: {roomId, host, port}
  → Redis SET game_transfer:{userId} = JSON{roomId, host, port, gameType} TTL 5분
  → SMatchFound{host, port, roomId} 전송
```
- `HttpClient`를 DI로 주입 (`Program.cs`에 `AddHttpClient` 등록)
- Matching.API 주소는 `appsettings.json` 또는 `Consts.cs`에 상수로 관리
- 매칭 실패(타임아웃/오류) 시 `SMatchFound` 대신 오류 코드 응답 패킷 추가

**1-4. Consts.cs 상수 추가**
```csharp
public const string GAME_TRANSFER_KEY_PREFIX = "game_transfer:";
```

**1-5. Tests.Game.Server → Tests.Game.Lobby 리네임**
- 프로젝트 파일, namespace, 어셈블리명 모두 업데이트
- `CMatchRequest` 핸들러 통합 테스트 추가 (Mock Matching.API HTTP 응답)

---

### 2. Game.Gomoku 로그인 흐름 변경

**2-1. CLogin 핸들러 추가 또는 수정**
기존 `active_user_key:{userId}` 확인 → `game_transfer:{userId}` 확인으로 교체:
```
CLogin{jwt, roomId}
  → JWT 검증
  → Redis GET game_transfer:{userId} → JSON 파싱
  → roomId 일치 여부 확인
  → 키 소비 (DEL)
  → 중복 로그인 락 획득
  → GomokuRoom 입장
```
- game_transfer 키가 없으면 `LoginNotInQueue` 오류 응답 + 연결 종료
- game_transfer의 gameType이 "gomoku"가 아니면 오류 응답

---

### 3. Matching.API 완성도 향상

**3-1. match_records Entity 및 Migration**

Entity:
```csharp
public class MatchRecord
{
    public int    Id            { get; set; }
    public int    PlayerAId     { get; set; }
    public int    PlayerBId     { get; set; }
    public int?   WinnerId      { get; set; }  // null = 무승부/미완료
    public string GameType      { get; set; } = string.Empty;
    public string RoomId        { get; set; } = string.Empty;
    public DateTime MatchedAt   { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int    PlayerARating { get; set; }  // 매칭 시점 MMR
    public int    PlayerBRating { get; set; }
}
```
- 테이블명: `match_records` (snake_case)
- Migration 이름: `CreateMatchRecordsTable`

**3-2. MMR 기반 매칭 알고리즘**
- 플레이어 기본 MMR: 1000 (신규 플레이어)
- 매칭 범위: ±100 MMR 이내 (대기 30초 초과 시 ±200으로 확장)
- MMR 저장 위치: Redis ZSet `matching:mmr` (score = MMR 값)
- ELO 공식으로 게임 결과 후 MMR 업데이트 (K=32)
- MMR 정보는 Auth DB `players` 테이블에 `rating` 컬럼 추가 또는 별도 Redis 관리

**3-3. 매칭 완료 시 처리**
- Matching.API가 매칭 성사 시 `POST /api/game-relay/match-created` 내부 API 제거
  → 대신 Redis Pub/Sub `game:match:created` 채널로 이벤트 발행 고려 (선택)
  → 우선 구현: Matching.API가 직접 Redis에 `game_transfer:{userA}`, `game_transfer:{userB}` 발급
- `match_records` INSERT (PlayerAId, PlayerBId, RoomId, MatchedAt, 각 MMR)

**3-4. GET /api/matching/history**
- 인증 필요 (`[Authorize]`)
- 요청 플레이어의 최근 매칭 이력 반환 (최대 20건)
- 응답: `[{ matchId, opponent, result, ratingChange, matchedAt }]`

**3-5. 테스트 보강 (12개 → 25개+)**
추가할 테스트 케이스:
- 매칭 요청 성공 (200)
- MMR 범위 내 상대 없음 → 대기 (202)
- 매칭 취소 (204)
- match_records 저장 확인
- GET /api/matching/history 인증 성공/실패
- MMR 업데이트 로직 유닛 테스트

---

## 영향 범위 (예상)

| 파일/경로 | 변경 종류 |
|-----------|---------|
| `PlatformA/PlatformA.Game.Server/` (전체) | 디렉토리 리네임 → `Game.Lobby` |
| `PlatformA/PlatformA.Game.Server/PlatformA.Game.Server.csproj` | 리네임 |
| `PlatformA/PlatformA.sln` | 프로젝트 참조 경로 업데이트 |
| `PlatformA.Library/Packets/Proto/packets.proto` | CMatchRequest, SMatchFound 추가 |
| `PlatformA.Library/Common/Consts.cs` | GAME_TRANSFER_KEY_PREFIX 추가 |
| `PlatformA.Game.Lobby/Packet/PacketHandler.cs` | CMatchRequest 핸들러 추가 |
| `PlatformA.Game.Lobby/Program.cs` | HttpClient DI 등록 |
| `PlatformA.Game.Gomoku/Packet/GomokuPacketHandler.cs` | CLogin 핐들러 수정 |
| `PlatformA.MySqlDB.Lib/DBWebApp/Entities/MatchRecord.cs` | 신규 |
| `PlatformA.MySqlDB.Lib/DBWebApp/DbWebAppContext.cs` | MatchRecord DbSet 추가 |
| `PlatformA.MySqlDB.Lib/Migrations/WebApp/` | CreateMatchRecordsTable Migration |
| `PlatformA.Matching.API/Services/MatchingService.cs` | MMR 알고리즘 추가 |
| `PlatformA.Matching.API/Controllers/MatchingController.cs` | /history 엔드포인트 추가 |
| `PlatformA.Tests.Game.Server/` → `Tests.Game.Lobby/` | 리네임 + 테스트 추가 |
| `PlatformA.Tests.Matching.API/` | 테스트 확장 |

---

## 제약 및 주의사항

- **ADR-007 준수**: 모든 새 패킷은 `packets.proto`에서만 정의, 수동 직렬화 금지
- **ADR-006 준수**: 매칭 큐는 Redis ZSet 사용, LPOP 방식 금지
- **CLAUDE.md 서비스 경계**: Game.Lobby → Matching.API는 HTTP 허용 (Lobby는 HTTP 클라이언트 역할). 단, Game.Lobby → Game.Gomoku 직접 HTTP 호출은 금지 — Redis game_transfer 티켓으로만 통신
- **Consts.cs 키 규칙**: `GAME_TRANSFER_KEY_PREFIX` 상수로만 사용, 하드코딩 금지
- **TTL 필수**: `game_transfer:*` 키 TTL 5분 (플레이어가 서버에 접속하지 않는 경우 자동 만료)
- **Migration 안전성**: `match_records` 테이블 신규 생성이므로 Down()에서 `DROP TABLE` 처리

---

## 구현 접근 방향

1. **리네임 우선**: 프로젝트 리네임 → 빌드 확인 → 이후 기능 추가 순서
2. **Proto 먼저**: `packets.proto` 수정 후 Protobuf 재생성 → 컴파일 오류 없음 확인
3. **Lobby 핸들러**: `HttpClient`는 `IHttpClientFactory` 패턴 사용 (`AddHttpClient<T>`)
4. **Matching 순서**: Entity → Migration → MMR 서비스 → 컨트롤러 → 테스트 순서
5. **Game.Gomoku**: login 핸들러 수정은 Lobby와 독립적으로 진행 가능

---

## 검증 기준

- [ ] `dotnet build PlatformA.sln` 오류 0개
- [ ] `dotnet test PlatformA.sln` 전체 통과
- [ ] PlatformA.Game.Lobby 프로젝트로 TCP :7777 정상 기동
- [ ] `CMatchRequest` 송신 시 SMatchFound 응답 수신 (Matching.API Mock 또는 실서버)
- [ ] Game.Gomoku에서 `game_transfer` 키 기반 로그인 성공
- [ ] Matching.API `match_records` 테이블에 매칭 결과 저장 확인
- [ ] `GET /api/matching/history` 정상 응답
- [ ] Tests.Matching.API 테스트 25개 이상
- [ ] Tests.Game.Lobby 핸들러 테스트 포함
