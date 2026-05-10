---
description: 패킷 구조체 코딩 규칙 — Google Protocol Buffers 기반 직렬화
globs: ["PlatformA/PlatformA.Library/Packets/**"]
---

# 패킷 코딩 규칙

- 패킷 정의는 `PlatformA.Library/Packets/Proto/packets.proto`에서만 관리한다 (ADR-003)
- C → S 패킷은 `C`로, S → C 패킷은 `S`로 이름을 시작한다 (예: `CMove`, `SLogin`)
- 새 패킷 추가 시: `packets.proto`에 message 추가 → `PacketID` enum에 ID 추가 → `PacketHandler.cs`에 핸들러 추가
- 수동 직렬화 절대 금지 — `BitConverter`, `BinaryReader`, `BinaryWriter` 직접 사용 금지
- 송신: `message.ToByteArray()` + 4바이트 헤더 조립 (`BuildResponsePacket` 헬퍼 사용)
- 수신: `CXxx.Parser.ParseFrom(buffer, offset, length)` — `InvalidProtocolBufferException` 로 파싱 실패 처리
- `[Packet]` 어트리뷰트, `partial struct`, `Size` 상수는 더 이상 사용하지 않는다 (Generator.Lib 제거됨)
- proto3 기본값 주의: 값이 0인 enum/int 필드는 wire에 포함되지 않음 (`LoginSuccess = 0` 등)
