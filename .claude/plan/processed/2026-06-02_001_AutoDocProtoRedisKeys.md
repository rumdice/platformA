# 요구사항 명세: AutoDocProtoRedisKeys

작성일: 2026-06-02
브랜치: 2026-06-02_AutoDocProtoRedisKeys
소스: task JSON summary

## 요구사항 요약

`packets.proto`와 `Consts.cs`를 파싱하여 `packet-protocol.md`의 패킷 목록·열거형 섹션과
`redis-keyspace.md`의 키 목록 섹션을 코드 변경 시 자동 갱신한다.
마커 기반 교체 방식으로 수동 작성 구간(시퀀스 다이어그램·설명 텍스트)은 보존한다.

## 상세 요구사항

### 1. `generate_proto_docs.py` 신규

**파싱 대상**: `PlatformA/PlatformA.Library/Packets/Proto/packets.proto`

파싱 항목:
- `enum Name { VALUE = N; }` → 결과 코드 열거형 테이블
- `message Name { type field = N; }` → 메시지별 필드 테이블
- `Packet { oneof payload { MsgType field_name = N; } }` → 패킷 목록 + oneof 태그 번호
- C/S prefix로 방향 구분 (C→S: `CXxx`, S→C: `SXxx`)
- `// 주석` 에서 방향 힌트·설명 추출 (message 선언 윗줄)

**갱신 대상** `Docs/developer-guide/packet-protocol.md`:

| 마커 | 갱신 내용 |
|---|---|
| `<!-- PACKET_LIST_START -->` ~ `<!-- PACKET_LIST_END -->` | C→S / S→C 패킷 목록 테이블 |
| `<!-- ENUM_LIST_START -->` ~ `<!-- ENUM_LIST_END -->` | LoginResultCode, EnterRoomResultCode 열거형 테이블 |

보존 구간 (마커 밖, 수정 없음):
- 와이어 포맷 다이어그램
- 로그인·이동 시퀀스 mermaid 다이어그램
- 직렬화/역직렬화 코드 예시
- 새 패킷 추가 절차

### 2. `generate_redis_key_docs.py` 신규

**파싱 대상**: `PlatformA/PlatformA.Library/Common/Consts.cs`

파싱 항목:
- `public const string XXX_KEY = "..."` → 키 패턴
- `public const string XXX_KEY_PREFIX = "..."` → 키 패턴 (접두사형)
- 윗줄 `// 주석` → 키 설명
- `public const int XXX_TTL_SECONDS` / `XXX_EXPIRY_DAYS` → TTL (초 환산)
- 키-TTL 상수 연결: `REFRESH_TOKEN_KEY_PREFIX` ↔ `REFRESH_TOKEN_EXPIRY_DAYS`

**갱신 대상** `Docs/architecture/redis-keyspace.md`:

| 마커 | 갱신 내용 |
|---|---|
| `<!-- REDIS_KEY_TABLE_START -->` ~ `<!-- REDIS_KEY_TABLE_END -->` | 전체 키 상수 테이블 (상수명·키 패턴·TTL·서비스) |

보존 구간 (마커 밖, 수정 없음):
- mermaid 전체 키 맵 다이어그램
- 키별 상세 명세 테이블
- Cluster 슬롯 설계 섹션

### 3. 문서 마커 삽입 (수동 1회)

`packet-protocol.md`: "## 패킷 목록" 섹션 전체를 마커로 감쌈
`redis-keyspace.md`: "## 전체 키 맵" 아래에 마커 구간 추가

### 4. `docs.yml` 스텝 추가

`generate_api_docs.py` 실행 전에:
```yaml
- name: Generate proto packet docs
  run: python .github/scripts/generate_proto_docs.py

- name: Generate Redis keyspace docs
  run: python .github/scripts/generate_redis_key_docs.py
```

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|---|---|
| `.github/scripts/generate_proto_docs.py` | **신규** |
| `.github/scripts/generate_redis_key_docs.py` | **신규** |
| `.github/workflows/docs.yml` | 스텝 2개 추가 |
| `Docs/developer-guide/packet-protocol.md` | 마커 삽입 + 자동 갱신 구간 교체 |
| `Docs/architecture/redis-keyspace.md` | 마커 삽입 + 자동 갱신 구간 교체 |

코드 변경 없음 — 서비스 동작 무영향.

## 제약 및 주의사항

- **proto 파서**: 외부 라이브러리 없이 정규식만 사용 (CI 환경 의존성 최소화)
- **마커 보존**: 교체 후 마커 자체는 문서에 유지 (다음 갱신 기준점)
- **fallback**: 파싱 실패 시 해당 마커 구간 내용을 교체하지 않고 보존
- **ADR-003 준수**: 키 문자열 하드코딩 없이 Consts.cs 상수를 단일 소스로 유지
- **ADR-007 준수**: proto 파일이 패킷 정의 단일 소스임을 문서 자동화로 강화

## 구현 접근 방향

```python
# generate_proto_docs.py 핵심 로직
import re
from pathlib import Path

def parse_proto(proto_path):
    src = proto_path.read_text(encoding="utf-8")
    # enum 파싱
    enums = re.findall(r'enum (\w+)\s*\{([^}]+)\}', src, re.DOTALL)
    # message 파싱
    messages = re.findall(r'message (\w+)\s*\{([^}]+)\}', src, re.DOTALL)
    # oneof 태그 파싱
    oneof = re.findall(r'(\w+)\s+(\w+)\s*=\s*(\d+);', oneof_body)
    return enums, messages, oneof

def update_markers(doc_path, marker, new_content):
    pattern = rf'<!--\s*{marker}_START\s*-->.*?<!--\s*{marker}_END\s*-->'
    replacement = f'<!-- {marker}_START -->\n{new_content}\n<!-- {marker}_END -->'
    return re.sub(pattern, replacement, content, flags=re.DOTALL)
```

```python
# generate_redis_key_docs.py 핵심 로직
def parse_consts(consts_path):
    src = consts_path.read_text(encoding="utf-8")
    # 키 상수 파싱: KEY / KEY_PREFIX 패턴
    keys = re.findall(
        r'//\s*(.+?)\n\s*public const string (\w+(?:KEY|PREFIX))\s*=\s*"([^"]+)"',
        src, re.DOTALL)
    # TTL 상수 파싱
    ttls = re.findall(r'public const int (\w+(?:SECONDS|DAYS|MINUTES))\s*=\s*(\d+)', src)
    return keys, ttls
```

## DESIGN_REVIEW 결과

| ADR | 관련 여부 | 충돌/참고 사항 |
|-----|---------|--------------|
| ADR-003: 하드코딩 금지 | 관련 있음 | Consts.cs를 단일 소스로 사용하는 원칙 강화 — 준수 ✅ |
| ADR-007: Protobuf 패킷 마이그레이션 | 관련 있음 | proto 파일을 문서 자동화 소스로 활용 — 준수 ✅ |
| ADR-005: Envelope 와이어 포맷 | 관련 없음 | — |
| ADR-001~004, 006 | 관련 없음 | — |

**판정: ✅ 기존 ADR 준수 — 신규 ADR 불필요**

## 검증 기준

1. `python .github/scripts/generate_proto_docs.py` 실행 후:
   - `packet-protocol.md`의 PACKET_LIST 마커 구간에 현재 proto 메시지 목록 반영
   - `packet-protocol.md`의 ENUM_LIST 마커 구간에 열거형 값 반영
2. `python .github/scripts/generate_redis_key_docs.py` 실행 후:
   - `redis-keyspace.md`의 REDIS_KEY_TABLE 마커 구간에 Consts.cs 키 상수 목록 반영
3. `packets.proto`에 새 메시지 추가 후 스크립트 재실행 → 문서에 자동 반영
4. `Consts.cs`에 새 Redis 키 추가 후 스크립트 재실행 → 문서에 자동 반영
5. GitHub Actions `docs.yml` 실행 성공 (오류 없음)
