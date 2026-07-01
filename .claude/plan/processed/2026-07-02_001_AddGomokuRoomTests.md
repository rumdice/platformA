# 요구사항 명세: AddGomokuRoomTests

작성일: 2026-07-02
브랜치: 2026-07-02_AddGomokuRoomTests
소스: plan mode (~/.claude/plans/7-1-flickering-cook.md)

## 요구사항 요약
GomokuRoom의 핵심 게임 오케스트레이션 로직(HandleDisconnect 3케이스, HandlePlaceStone 턴 검증·5목·무승부, FinishGame 중복 호출 가드)에 대한 단위 테스트 약 15개를 추가한다. 테스트 인프라(TestableGomokuRoom, FakeGomokuSession)를 신규 구축하며 프로덕션 코드는 변경하지 않는다.

## 상세 요구사항

1. **테스트 인프라 구축**
   - `FakeGomokuSession : GomokuSession` — 생성자에서 SessionId 설정, TCP 소켓 없이 인스턴스화 가능
   - `TestableGomokuRoom : GomokuRoom` — `Broadcast()` override로 패킷을 `BroadcastHistory` 리스트에 캡처

2. **HandleDisconnect 테스트 (5개)**
   - WaitingPlayers 상태: SGameOver 브로드캐스트, GameState는 WaitingPlayers 유지
   - InProgress 상태: GameState → Finished, SGameOver.WinnerId == 상대방 ID
   - Finished 상태: BroadcastHistory 추가 없음 (가드 작동)

3. **HandlePlaceStone 테스트 (8개)**
   - 순서 아닌 플레이어 요청 → 무시 (BroadcastHistory 변화 없음)
   - 이미 돌 있는 위치 → 무시
   - 유효한 수 → 다음 플레이어 턴으로 전환, SBoardUpdate 브로드캐스트
   - 5목 완성 → GameState == Finished, SGameOver.WinnerId == 돌 놓은 플레이어 ID
   - 보드 가득 참(225칸) → SGameOver.WinnerId == 0 (무승부)
   - GameState != InProgress 상태에서 요청 → 조기 반환

4. **FinishGame 가드 테스트 (2개)**
   - HandlePlaceStone(5목) 후 HandleDisconnect → SGameOver 1회만 브로드캐스트
   - Enter 2회 → SGameStart 패킷 브로드캐스트 확인

## 영향 범위 (예상)

| 파일 | 변경 종류 |
|------|---------|
| `PlatformA.Tests.Game.Gomoku/Helpers/TestableGomokuRoom.cs` | 신규 |
| `PlatformA.Tests.Game.Gomoku/Helpers/FakeGomokuSession.cs` | 신규 |
| `PlatformA.Tests.Game.Gomoku/GomokuRoomDisconnectTests.cs` | 신규 |
| `PlatformA.Tests.Game.Gomoku/GomokuRoomPlaceStoneTests.cs` | 신규 |
| `PlatformA.Tests.Game.Gomoku/GomokuRoomFinishGameGuardTests.cs` | 신규 |
| `.claude/rules/tests.md` | 수정 (테스트 수 현황 업데이트) |

프로덕션 코드 변경 없음.

## 제약 및 주의사항

- 기존 Gomoku 테스트 프로젝트는 Moq 없음 — 추가하지 않음
- InternalsVisibleTo 없음 — 모든 대상 메서드가 public이므로 불필요
- `GomokuRoom._board` Reflection 접근: 무승부 테스트에서 224개 칸 사전 채우기에 사용 (기존 GomokuRoomLoggerTests의 Reflection 패턴과 일관됨)
- `ReportMatchResultAsync()`: fire-and-forget, HTTP 실패 시 Console.Error 출력만 — 테스트 차단 없음
- Task.Run 타임아웃 루프: GameState == Finished 감지 시 자동 종료 — 테스트 간섭 없음
- `GomokuRoomManager.Instance.Remove()`: Guid roomId로 직접 생성 시 no-op — 안전

## 구현 접근 방향

1. `TestableGomokuRoom.Broadcast()` override → TCP 없이 패킷 캡처
2. `FakeGomokuSession(int playerId)` → `SessionId = playerId` 설정, 상속으로 타입 체크 통과
3. 테스트 세팅: `room.Push(() => { room.Enter(p1); room.Enter(p2); })` — Push가 동기 실행이므로 즉시 GameState == InProgress
4. 패킷 파싱: `Packet.Parser.ParseFrom(bytes.Skip(2).ToArray())`
5. 5목 좌표: P1(7,7)(8,7)(9,7)(10,7)(11,7) 가로 5연속, P2(0,0)(1,0)(2,0)(3,0) 비간섭

## 검증 기준

- `dotnet test PlatformA/PlatformA.Tests.Game.Gomoku/PlatformA.Tests.Game.Gomoku.csproj -q` → 기존 52 + 신규 ~15 = 약 67개 통과
- `dotnet test PlatformA.sln -q` → 전체 솔루션 회귀 없음
- 모든 HandleDisconnect 케이스(3가지), HandlePlaceStone 케이스(8가지), FinishGame 가드(2가지) 테스트 포함
