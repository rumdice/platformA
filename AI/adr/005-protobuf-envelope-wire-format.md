# ADR-005: TCP 와이어 포맷 — Protobuf Envelope (4바이트 → 2바이트 헤더)

## 상태: 확정

## 날짜: 2026-05-11

## 부분 대체: ADR-007 (§ "TCP 헤더 변경 없음" 조항 폐기)

---

## 맥락

ADR-007에서 Protobuf로 전환하면서 TCP 프레임 헤더를 의도적으로 그대로 유지했다:

```
기존(ADR-002/003): [ushort size: 2B LE][ushort packetId: 2B LE][Protobuf payload: NB]
```

이 구조에는 두 가지 문제가 남아 있었다.

1. **수동 packetId 관리**: `BuildResponsePacket`, `BuildPacket`, `PacketHandlerAttribute(ushort)` 모두 `PacketID` enum을 캐스팅해야 했다. 패킷 추가 시 enum 값을 수동으로 맞춰야 하는 오류 가능성이 있었다.

2. **DummyClient TCP 단편화 버그**: 4개 시나리오 파일 모두 `socket.ReceiveAsync`가 반환한 `received` 바이트 수를 그대로 사용했다. TCP는 데이터를 여러 조각으로 나눠 전달할 수 있으므로 `received < frameSize` 상태에서 파싱하면 `InvalidProtocolBufferException`이 발생한다.

---

## 결정

`Packet { oneof payload { ... } }` envelope 메시지를 도입해 packetId 헤더를 제거한다.

### 새 와이어 포맷

```
[ushort size: 2B LE][Packet envelope protobuf: NB]
```

- `size` = 2 + `envelope.ToByteArray().Length`
- 헤더가 4B → 2B로 줄어 패킷당 2바이트 절약
- `packetId`는 protobuf `oneof` field tag(1~6)로 대체 — 컴파일 타임 타입 체크

### proto 변경 (`packets.proto`)

```proto
message Packet {
    oneof payload {
        CMove      c_move       = 1;
        SMove      s_move       = 2;
        CLogin     c_login      = 3;
        SLogin     s_login      = 4;
        CEnterRoom c_enter_room = 5;
        SEnterRoom s_enter_room = 6;
    }
}
```

`Packet.PayloadOneofCase` 값(1~6)이 기존 `PacketID` enum 값(1~6)과 정확히 일치한다.

### 변경된 컴포넌트

| 컴포넌트 | 변경 |
|---------|------|
| `Session.cs` (Library) | **변경 없음** — `TryReadPacket`의 2바이트 size 읽기 그대로 |
| `PacketManager.cs` | `ushort` → `PayloadOneofCase` 키; Delegate `ReadOnlySpan<byte>` → `Packet` |
| `GameSession.OnRecv` | offset 4 → 2에서 `Packet.Parser.ParseFrom` |
| `PacketHandler.cs` | `[PacketHandler(Packet.PayloadOneofCase.X)]`; 핸들러가 `Packet` 직접 접근 |
| `PacketHelper.cs` (DummyClient) | `BuildPacket(Packet)`, `ReceiveFrameAsync` — size 헤더 기반 루프로 단편화 수정 |
| DummyClient 시나리오 4개 | `received < 4` + `BitConverter` 패턴 → `ReceiveFrameAsync` + `ParseEnvelope` |
| `PacketFramingTests.cs` | envelope 기반 테스트로 전면 교체 |

### 네임스페이스 충돌 처리

`PlatformA.Game.Server.Packet` 네임스페이스가 `Packet` 클래스를 가려, `PacketHandler.cs`와 `GameSession.cs`에서 `using ProtoPacket = PlatformA.Library.Packets.Packet;` 별칭을 사용한다.

### PacketID enum 처리

- `PacketID` enum 값(1~6)은 `Packet.PayloadOneofCase`와 동일 → 사용처 없음
- 이번 PR: `[Obsolete("Use Packet.PayloadOneofCase")]` 추가 — 컴파일 경고 발생
- 삭제는 다음 PR에서 진행

---

## 결과

- **긍정**: packetId 수동 관리 제거; TCP 단편화 버그 수정; 새 패킷 추가 시 proto 파일 한 곳만 수정
- **부정**: `PlatformA.Game.Server.Packet` 네임스페이스 이름이 `Packet` 클래스와 충돌 — 별칭 필요 (향후 네임스페이스 이름 변경으로 해소 가능)
- **무효화**: ADR-007의 "TCP 헤더 4바이트 구조 유지" 조항은 이 결정으로 폐기됨
