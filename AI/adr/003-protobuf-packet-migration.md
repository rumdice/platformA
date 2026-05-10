# ADR-003: 패킷 직렬화 — Google Protocol Buffers 전환

## 상태: 확정

## 날짜: 2026-05-11

## 대체: ADR-002 (Custom Binary + Source Generator)

---

## 맥락

ADR-002에서 채택한 Custom Binary + Roslyn Source Generator 방식은 아래 문제를 유발했다.

1. **수동 직렬화 코드 유지 부담**: DummyClient에서 `BitConverter`로 패킷 헤더와 페이로드를 직접 조립해야 했고, 오프셋 오류가 발생하기 쉬웠다.
2. **Source Generator 유지 비용**: `PlatformA.Generator.Lib`는 `[Packet]` 어트리뷰트를 스캔해 `partial struct`에 `Serialize`/`Deserialize`를 삽입하는 Roslyn 기반 코드 생성기였다. 새 패킷 추가 시 Generator, struct, enum 3곳을 동시에 수정해야 했다.
3. **필드 추가의 복잡성**: 기존 `Size` 상수에 맞춰 오프셋을 수동 계산해야 해서, 필드 하나 추가 시 Generator 로직과 Size 상수를 함께 변경해야 했다.

반면, 이미 팀이 Protobuf 방식의 round-trip 테스트를 작성해두어 전환 검증이 준비되어 있었다.

---

## 결정

**Google Protocol Buffers 3 (proto3) 채택**

### 변경 범위

| 구성요소 | 변경 내용 |
|---------|---------|
| 페이로드 포맷 | Custom binary → Protobuf 인코딩 |
| `PlatformA.Generator.Lib` | 완전 제거 |
| `PlatformA.Library.csproj` | `Google.Protobuf 3.29.3` + `Grpc.Tools 2.67.0` 추가, `<Protobuf>` 아이템 등록 |
| `Packets/Proto/packets.proto` | 신규 — 모든 메시지 + enum 정의 |

### 불변 구성요소 (변경 없음)

| 구성요소 | 이유 |
|---------|------|
| TCP 4바이트 헤더 (`ushort size | ushort packetId`) | 네트워크 레이어 코드 그대로 유지 |
| `PacketID` enum 값 | 클라이언트와의 호환성 |
| `PacketManager` 리플렉션 디스패치 | O(1) 런타임 성능 유지 |
| `[PacketHandler]` 어트리뷰트 기반 핸들러 등록 | 구조 변경 없음 |

### proto3 파일 위치

```
PlatformA.Library/Packets/Proto/packets.proto
```

Grpc.Tools가 빌드 시 `obj/` 아래에 C# 클래스를 자동 생성한다 (미커밋).

### 직렬화 API

```csharp
// 송신
byte[] payload = message.ToByteArray();

// 수신
var msg = CLogin.Parser.ParseFrom(buffer, offset, length);
```

### 공통 응답 헬퍼 (PacketHandler.cs)

```csharp
private static byte[] BuildResponsePacket(PacketID id, IMessage message)
{
    byte[] payload = message.ToByteArray();
    ushort size = (ushort)(4 + payload.Length);
    byte[] buf = new byte[size];
    BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), size);
    BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2, 2), (ushort)id);
    payload.CopyTo(buf, 4);
    return buf;
}
```

---

## 대안과 기각 이유

| 대안 | 기각 이유 |
|------|---------|
| Source Generator 유지 | 유지 부담 ↑, 필드 추가 시 3곳 동시 수정 |
| MessagePack | protobuf보다 .NET 생태계 지원 좁음 |
| JSON | 크기 및 파싱 오버헤드, 실시간 게임 패킷에 부적합 |
| FlatBuffers | zero-copy 이점 있으나 팀 학습 비용 > 이득 |

---

## 결과 및 트레이드오프

**이득:**
- 패킷 정의가 `.proto` IDL 한 파일로 집중 — 새 패킷 추가 시 enum + proto 2곳만 수정
- DummyClient 시나리오 코드 간소화 (`BitConverter` 직접 조작 제거)
- 언어 중립 IDL — 향후 Go/Unity 클라이언트 연동 시 proto 재사용 가능
- `Parser.ParseFrom` 예외(`InvalidProtocolBufferException`)로 명시적 파싱 실패 처리

**비용:**
- proto3 기본값 동작 주의: 값이 0인 enum/int32 필드는 wire에 포함되지 않는다. 예) `LOGIN_SUCCESS = 0`은 전송되지 않으며 수신 측은 기본값(0 = 성공)으로 복원. 의미상 동일하나 혼동 가능.
- 가변 길이 페이로드: 고정 `Size` 상수 제거, 수신 시 `received < 4` 가드 후 protobuf 예외로 처리
- `Grpc.Tools` Windows protoc 빌드 도구 의존 (NuGet 캐시 자동 사용, PATH 설정 불필요)

---

## 새 패킷 추가 절차

1. `packets.proto`에 `message` / `enum` 추가
2. `Packet.cs`의 `PacketID` enum에 새 ID 추가
3. `PacketHandler.cs`에 `[PacketHandler]` 핸들러 추가
4. 해당 패킷 round-trip 테스트 추가
