# ADR-002: Game Server Binary 패킷 프로토콜 + Source Generator

## 상태: 폐기 (Superseded by ADR-003 — 2026-05-11)

## 날짜: 2026-04-21

---

## 맥락

Game Server는 동시 1,000개 이상의 TCP 연결을 처리해야 함.
각 연결에서 실시간으로 위치/상태 업데이트 패킷이 초당 다수 발생.

선택지:
- JSON (텍스트 기반): 파싱 오버헤드, 메모리 할당 많음
- Protobuf: 외부 의존성, 코드 생성 도구 필요
- Custom Binary: 최소 크기, 제로 할당 가능, 완전한 제어권

---

## 결정

**커스텀 Binary 패킷 프로토콜 + C# Source Generator**

### 패킷 구조
```
[Header: 4 bytes] + [Payload: N bytes]
 ├── Length: 2 bytes (ushort, little-endian) — 전체 패킷 크기
 └── PacketID: 2 bytes (ushort, little-endian) — 패킷 종류
```

### Source Generator 동작
- `[Packet]` 어트리뷰트가 붙은 `partial struct`에 `Serialize()` / `Deserialize()` 메서드를 컴파일 타임에 자동 생성
- `Span<byte>` 기반으로 힙 할당 없음
- `BinaryPrimitives` 사용 (엔디안 명시적 제어)

### 네트워크 레이어
- `System.IO.Pipelines`: 버퍼 단편화 없는 스트리밍 처리
- `ArrayPool<byte>`: 전송 버퍼 재사용 (GC 압력 최소화)
- `PacketManager`: 리플렉션으로 핸들러 탐색 (시작 시 1회) → 런타임은 컴파일된 delegate

---

## 대안과 기각 이유

| 대안 | 기각 이유 |
|------|---------|
| JSON (System.Text.Json) | 1,000 동시 접속 시 파싱 오버헤드, 텍스트 직렬화 불필요 |
| MessagePack | 외부 패키지 의존성. Binary보다 크기 큼 (타입 정보 포함) |
| Protobuf | 스키마 파일(.proto) 별도 관리 필요. 오버엔지니어링 |
| SignalR (WebSocket) | 게임 서버는 ASP.NET Core 없이 순수 Console App 유지하는 것이 목적 |

---

## 결과 및 트레이드오프

**이득:**
- 패킷당 할당 0 (스택 기반 `Span<byte>`)
- 패킷 크기 최소화 (Move: 12바이트 vs JSON ~50바이트)
- 핸들러 추가 시 boilerplate 없음 (`[PacketHandler]` 어트리뷰트만)

**비용:**
- 새 패킷 추가 시 `PacketID` enum 등록 + `[Packet]` struct 정의 + `[PacketHandler]` 핸들러 3단계 필수
- 디버깅 시 바이너리 데이터 사람이 읽기 어려움
- DummyClient로 통합 테스트 필수 (유닛 테스트로 커버 어려움)

---

## 변경 방법

이 결정을 변경하려면:
1. 새 ADR 작성
2. 사용자 승인 후 진행
3. Game Server 전체 네트워크 레이어 재작성 필요 (범위 큼)
