# Plan: Generator.Lib 삭제 및 패킷 패턴 문서 정리

## Context

ADR-003(2026-05-11)에서 패킷 직렬화 방식을 Roslyn Source Generator → Google Protocol Buffers 3로 전환했다.
그 결과 `PlatformA.Generator.Lib` 프로젝트는 소스 파일이 모두 삭제되어 빈 디렉토리만 남아있고,
`PlatformA.sln`에도 등록되지 않았으며 어떤 프로젝트도 참조하지 않는 상태다.

아울러 `AI/PATTERNS.md`의 패킷 추가 패턴이 여전히 구(Source Generator) 방식으로 기술되어 있어,
새 패킷 추가 시 혼란을 야기할 수 있다.

---

## 조사 결과 요약

### 패킷 생성 자동화 에이전트/스킬 현황
| 스킬 | 역할 | 패킷 자동화 여부 |
|------|------|----------------|
| `/doc-writer developer-guide` | proto → 문서 자동 생성 | 문서 생성만 |
| `/run-scenarios` | 실제 패킷 송수신 테스트 | 테스트만 |
| **proto 기반 패킷 추가 자동화 스킬** | — | **없음** |

→ 새 패킷 추가 워크플로(proto 편집 → 핸들러 작성)를 자동화하는 전용 스킬은 없다.  
→ 단, 현재 `Grpc.Tools`가 빌드 타임에 proto → C# 클래스를 자동 생성하므로 별도 생성 Agent는 불필요.

### PlatformA.Generator.Lib 상태
- **디렉토리**: 존재하지만 **완전히 비어 있음** (파일 0개)
- **sln 등록**: 없음
- **ProjectReference**: 없음
- **사용 패턴** (`[Packet]`, `partial struct`, `PacketAutoGenerate`): 코드베이스 전체에서 0건
- **결론**: 완전히 불필요 — 삭제 대상

---

## 작업 범위

### 필수 (직접 요청)
1. **빈 디렉토리 삭제**
   - `PlatformA/PlatformA.Generator.Lib/` 디렉토리 제거

### 연계 정리 (불일치 제거)
2. **`AI/PATTERNS.md` 패킷 섹션 업데이트**
   - 현재: Source Generator 기반 3단계 패턴 (`[Packet]` 어트리뷰트 등) → 실제로는 삭제된 방식
   - 변경: Protobuf 기반 패턴으로 교체
     ```
     Step 1: packets.proto에 message 추가 + Packet.oneof에 필드 등록
     Step 2: PacketHandler.cs에 핸들러 메서드 추가 ([PacketHandler] 어트리뷰트)
     Step 3: 빌드 — Grpc.Tools가 C# 클래스 자동 생성
     ```

3. **`PlatformA/PlatformA.Library/Packets/Packet.cs` 정리**
   - `[Obsolete]` 마킹된 `PacketID` enum 제거 (Packet.cs 주석에 "will be removed in a future PR" 명시됨)

---

## 변경 대상 파일

| 파일 | 변경 유형 |
|------|----------|
| `PlatformA/PlatformA.Generator.Lib/` | 디렉토리 삭제 |
| `AI/PATTERNS.md` | 패킷 추가 패턴 섹션 교체 |
| `PlatformA/PlatformA.Library/Packets/Packet.cs` | `[Obsolete]` PacketID enum 제거 |

---

## 검증 절차

```bash
# 1. 빌드 — 오류 0개 확인
cd PlatformA && dotnet build PlatformA.sln

# 2. Generator.Lib 디렉토리 부재 확인
ls PlatformA/PlatformA.Generator.Lib  # 없어야 함

# 3. PacketID 참조 부재 확인 (제거 전 수행)
grep -r "PacketID" PlatformA/ --include="*.cs"
# → Packet.cs 외 다른 곳에서 사용 여부 확인 후 제거
```
