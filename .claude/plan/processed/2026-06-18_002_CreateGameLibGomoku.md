# 요구사항 명세: CreateGameLibGomoku

작성일: 2026-06-18
브랜치: 2026-06-18_CreateGameLibGomoku
소스: 사용자 지시 + 2026-06-18 아키텍처 설계 대화

## 요구사항 요약

게임 서버 아키텍처를 Option B-1로 전환한다.
1. `PlatformA.Library.Game` 신규 라이브러리 프로젝트 생성 — 현재 `Game.Server`에 혼재된 TCP/Room/Session 인프라를 공통 레이어로 추출
2. `PlatformA.Game.Gomoku` 신규 TCP 서버 프로젝트 생성 — Library.Game 위에 오목 게임 도메인 로직 구현

## 상세 요구사항

### 1. PlatformA.Library.Game 프로젝트 생성

- **위치**: `PlatformA/PlatformA.Library.Game/`
- **참조**: `PlatformA.Library` (기존)
- **이동할 파일** (Game.Server → Library.Game, 네임스페이스 변경):
  - `GameSession.cs` (`PlatformA.Game.Server.Network` → `PlatformA.Library.Game.Network`)
  - `GameRoom.cs` (`PlatformA.Game.Server.Core` → `PlatformA.Library.Game.Core`)
  - `GameRoomManager.cs` (`PlatformA.Game.Server.Core` → `PlatformA.Library.Game.Core`)
- `PlatformA.Game.Server`에 `PlatformA.Library.Game` 참조 추가, 네임스페이스 using 교체

### 2. PlatformA.Game.Gomoku 프로젝트 생성

- **위치**: `PlatformA/PlatformA.Game.Gomoku/`
- **참조**: `PlatformA.Library.Game`, `PlatformA.Library`, `PlatformA.MySqlDB.Lib`
- **포트**: 7778 (Game.Server는 7777 유지)
- **구성 파일**:

```
PlatformA.Game.Gomoku/
  Program.cs                    — TCP 서버 진입점 (포트 7778)
  Network/
    GomokuSession.cs            — GameSession 상속, Gomoku 전용 세션
  Core/
    GomokuRoom.cs               — GameRoom 상속, 게임 상태 관리
    Board.cs                    — 15×15 바둑판
    WinChecker.cs               — 5연속 승패 판정
    TurnManager.cs              — 플레이어 교대 및 타임아웃
  Packet/
    GomokuPacketHandler.cs      — CPlaceStone 처리
```

### 3. Protobuf 패킷 추가 (packets.proto)

```protobuf
// Gomoku 전용 패킷
enum StoneColor { STONE_NONE = 0; STONE_BLACK = 1; STONE_WHITE = 2; }
enum GameOverReason { FIVE_IN_ROW = 0; TIMEOUT = 1; DISCONNECT = 2; }

message CPlaceStone   { int32 x = 1; int32 y = 2; }
message SBoardUpdate  { int32 x = 1; int32 y = 2; StoneColor color = 3; int32 next_turn_player_id = 4; }
message SGameStart    { int32 player1_id = 1; int32 player2_id = 2; int32 first_turn_player_id = 3; }
message SGameOver     { int32 winner_id = 1; GameOverReason reason = 2; }
```

Packet.oneof에 4개 필드 추가 (필드 번호 7~10).

### 4. 솔루션 파일 업데이트

`PlatformA.sln`에 두 신규 프로젝트 등록.

## 영향 범위 (예상)

| 파일/프로젝트 | 변경 유형 | 이유 |
|-------------|---------|------|
| `PlatformA.Library.Game/` (신규) | 생성 | 게임 서버 공통 인프라 레이어 |
| `PlatformA.Game.Gomoku/` (신규) | 생성 | 오목 게임 도메인 서버 |
| `PlatformA.Game.Server/Core/GameRoom.cs` | 삭제 후 Library.Game 참조 | 이동 |
| `PlatformA.Game.Server/Core/GameRoomManager.cs` | 삭제 후 Library.Game 참조 | 이동 |
| `PlatformA.Game.Server/Network/GameSession.cs` | 삭제 후 Library.Game 참조 | 이동 |
| `PlatformA.Game.Server/Packet/PacketHandler.cs` | using 교체 | 네임스페이스 변경 |
| `PlatformA.Library/Packets/Proto/packets.proto` | 수정 | Gomoku 패킷 추가 |
| `PlatformA.sln` | 수정 | 신규 프로젝트 2개 등록 |

## 제약 및 주의사항

- **ADR-007 준수**: 패킷 정의는 `packets.proto`에서만 관리, 수동 직렬화 금지
- **proto3 기본값 주의**: `StoneColor.STONE_NONE = 0` — wire에서 생략됨 (정상)
- **Game.Server 유지**: 기존 Game.Server는 PacketHandler 포함 유지 — 레거시/데모용. Library.Game 참조로 교체하되 삭제하지 않음
- **포트 분리**: Gomoku=7778, Game.Server=7777 (충돌 없음)
- **테스트**: `PlatformA.Tests.Game.Server`는 Library.Game 참조로 교체 필요

## 구현 접근 방향

1. `PlatformA.Library.Game.csproj` 생성 → `GameSession/GameRoom/GameRoomManager` 파일 복사 → 네임스페이스 변경
2. `PlatformA.Game.Server.csproj`에 Library.Game 참조 추가, 원본 파일 삭제, using 교체
3. `packets.proto`에 Gomoku 패킷 4종 추가 → dotnet build로 코드 자동 생성 확인
4. `PlatformA.Game.Gomoku.csproj` 생성 → Program.cs (TCP 7778) → GomokuSession → GomokuRoom → Board → WinChecker → TurnManager → GomokuPacketHandler
5. `PlatformA.sln`에 두 프로젝트 등록
6. 전체 빌드 확인

## 검증 기준

1. `dotnet build PlatformA.sln` — 오류 0개
2. `dotnet test PlatformA.sln` — 기존 133개 테스트 전부 통과 (회귀 없음)
3. `PlatformA.Game.Server`가 기존과 동일하게 빌드됨 (네임스페이스만 변경)
4. `PlatformA.Game.Gomoku`가 빌드됨 (신규 프로젝트)
5. `PlatformA.Library.Game`이 빌드됨 (신규 프로젝트)
6. packets.proto의 Gomoku 패킷 4종이 C# 클래스로 생성됨
