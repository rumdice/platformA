---
description: 패킷 구조체 코딩 규칙 — Source Generator 기반 직렬화 강제
globs: ["PlatformA/PlatformA.Library/Packets/**", "PlatformA/PlatformA.Generator.Lib/**"]
---

# 패킷 코딩 규칙

- `[Packet]` 어트리뷰트 필수 — Source Generator(`PlatformA.Generator.Lib`)가 직렬화 코드를 자동 생성한다
- `partial struct` 선언 필수 — 제너레이터가 partial 클래스에 코드를 추가한다
- `public const ushort Size` 필드 필수 — 파이프라인에서 패킷을 자를 때 사용 (float 3개 = 12바이트, int = 4바이트)
- 수동 직렬화 절대 금지 — `BitConverter`, `BinaryReader`, `BinaryWriter` 직접 사용 금지
- C → S 패킷은 `C_`로, S → C 패킷은 `S_`로 이름을 시작한다
- 새 패킷 추가 시 `AI/PATTERNS.md` "패킷 추가 패턴" 섹션을 반드시 참조한다
- `PlatformA.Library.csproj`에서 Generator.Lib는 `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`로 참조한다 — 어셈블리로 직접 링크되지 않음에 주의
