# 요구사항 명세: AutoUpdateDocMeta

작성일: 2026-05-30
브랜치: 2026-05-30_AutoUpdateDocMeta
소스: task JSON summary + 코드 탐색 결과

## 요구사항 요약

.NET 버전과 테스트 수가 문서에 하드코딩되어 코드 변경 시 자동으로 반영되지 않는 문제를 해결한다.
`generate_api_docs.py`의 SERVICES 리스트와 `Docs/index.md`, `Docs/architecture/overview.md`를
csproj XML 파싱 및 테스트 파일 카운팅 기반으로 동적 갱신하도록 개선한다.

## 발견된 불일치 (현재 상태)

| 항목 | 코드 실제 | 문서 표시 | 위치 |
|------|---------|---------|------|
| .NET 버전 (Auth/Ticketing/Utils) | net10.0 | `.NET 8.0` | `generate_api_docs.py` SERVICES, `overview.md` |
| .NET 버전 (Matching) | net10.0 | `.NET 9.0` | 동일 |
| 테스트 수 | 125개 | `97개` | `Docs/index.md` |
| 기술 스택 런타임 표기 | .NET 10.0 | `.NET 8.0 / .NET 9.0` | `Docs/index.md` |

## 상세 요구사항

### 1. `generate_api_docs.py` — SERVICES .NET 버전 동적 화

현재:
```python
SERVICES = [
    {"name": "Auth API", "runtime": ".NET 8.0", ...},
    {"name": "Matching API", "runtime": ".NET 9.0", ...},
]
```

변경 후:
- 각 API 프로젝트의 `.csproj` 파일을 XML 파싱하여 `<TargetFramework>` 값 읽기
- `net10.0` → `.NET 10.0` 형태로 변환하여 SERVICES에 주입
- 파싱 실패 시 기존 SERVICES 값 fallback (안전성 유지)

### 2. `generate_doc_meta.py` (신규) — index.md 동적 갱신

아래 두 값을 코드에서 자동 계산하여 `Docs/index.md`를 갱신한다:

**2-1. .NET 버전 표기**
- 모든 csproj에서 `<TargetFramework>` 값 수집
- 고유값 집합 → 단일이면 `.NET X.0`, 복수이면 `.NET X.0 / .NET Y.0` 형태
- `Docs/index.md` 내 마커 `<!-- RUNTIME_VERSION -->` 사이 내용 교체

**2-2. 테스트 수 자동 카운팅**
- `PlatformA.Tests.*` 디렉토리 내 `*.cs` 파일에서 `[Fact]`, `[Theory]` 어트리뷰트 카운팅
- 주석 내 발생(`//`, `///`, `/* */`) 제외
- `Docs/index.md` 내 마커 `<!-- TEST_COUNT -->` 사이 내용 교체

### 3. `Docs/architecture/overview.md` — 런타임 버전 테이블 자동 갱신

현재 수동 관리되는 테이블:
```markdown
| Auth API | .NET 8.0 | |
| Matching API | **.NET 9.0** | 최신 성능 기능 사용 |
```

변경 후:
- `generate_doc_meta.py`에서 각 서비스별 csproj 파싱 → 버전 주입
- 마커 `<!-- RUNTIME_TABLE_START -->` ~ `<!-- RUNTIME_TABLE_END -->` 사이 테이블 교체
- 비고(Remark) 컬럼은 고정 문자열 사전 유지 (자동 판단 어려움)

### 4. `docs.yml` — 새 스크립트 실행 스텝 추가

`generate_api_docs.py` 실행 전 또는 후에:
```yaml
- name: Update doc metadata (version + test count)
  run: python3 .github/scripts/generate_doc_meta.py
```

### 5. `Docs/index.md` — 마커 삽입

갱신 대상 라인에 HTML 주석 마커 추가:
```markdown
- **런타임**: <!-- RUNTIME_VERSION -->`.NET 10.0`<!-- /RUNTIME_VERSION -->
- **테스트**: xUnit · <!-- TEST_COUNT -->125개<!-- /TEST_COUNT --> 테스트
```

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|------|---------|
| `.github/scripts/generate_api_docs.py` | csproj XML 파싱 로직 추가 |
| `.github/scripts/generate_doc_meta.py` | **신규** |
| `.github/workflows/docs.yml` | 스텝 1개 추가 |
| `Docs/index.md` | 마커 삽입 (수동 1회) + 이후 자동 갱신 |
| `Docs/architecture/overview.md` | 마커 삽입 (수동 1회) + 이후 자동 갱신 |

## 제약 및 주의사항

- **ADR-003 준수**: 하드코딩 제거 원칙 — 이 변경이 그 원칙을 문서 도구에도 적용하는 것
- **fallback 필수**: csproj 파싱 실패 시 기존 값을 유지하여 문서 빌드가 깨지지 않도록
- **마커 형식**: HTML 주석(`<!-- ... -->`) 사용 — DocFX 렌더링 시 보이지 않음
- **테스트 카운팅**: 주석 내 어트리뷰트 제외 필수 (오탐 방지)
- **단위 변경 없음**: 기존 generate_api_docs.py 실행 결과는 변경 없어야 함 (SERVICES 구조 유지)

## 구현 접근 방향

```python
# generate_doc_meta.py 핵심 로직

import xml.etree.ElementTree as ET, pathlib, re

def get_dotnet_version(csproj_path: str) -> str:
    """csproj에서 TargetFramework 읽어 '.NET X.0' 형태로 변환"""
    tree = ET.parse(csproj_path)
    tf = tree.find('.//{*}TargetFramework')
    if tf is not None and tf.text:
        # net10.0 → .NET 10.0
        version = re.sub(r'^net(\d+)\.(\d+)$', r'.NET \1.\2', tf.text)
        return version
    return ".NET ?"

def count_tests(tests_dir: str) -> int:
    """[Fact]/[Theory] 어트리뷰트 카운팅 (주석 제외)"""
    count = 0
    for cs in pathlib.Path(tests_dir).rglob("*.cs"):
        content = cs.read_text(encoding="utf-8")
        # 라인 주석, 블록 주석 제거 후 카운팅
        content = re.sub(r'//.*$', '', content, flags=re.MULTILINE)
        content = re.sub(r'/\*.*?\*/', '', content, flags=re.DOTALL)
        count += len(re.findall(r'\[(?:Fact|Theory)\]', content))
    return count

def update_marker(content: str, marker: str, new_value: str) -> str:
    """<!-- MARKER -->...<!-- /MARKER --> 사이 내용 교체"""
    pattern = rf'<!--\s*{marker}\s*-->.*?<!--\s*/{marker}\s*-->'
    replacement = f'<!-- {marker} -->{new_value}<!-- /{marker} -->'
    return re.sub(pattern, replacement, content, flags=re.DOTALL)
```

## DESIGN_REVIEW 결과

| ADR | 관련 여부 | 충돌/참고 사항 |
|-----|---------|--------------|
| ADR-003: 하드코딩 금지 | 관련 있음 | 이 변경이 ADR-003 원칙을 문서 도구에 적용하는 것 — 준수 ✅ |
| ADR-001, 002, 004~007 | 관련 없음 | — |

**판정: ✅ 기존 ADR 준수 — 신규 ADR 불필요**

## 검증 기준

1. `python3 .github/scripts/generate_doc_meta.py` 실행 후:
   - `Docs/index.md`: `.NET 10.0`, `125개 테스트` 반영
   - `Docs/architecture/overview.md`: 런타임 버전 테이블 전 서비스 `.NET 10.0`
2. `python3 .github/scripts/generate_api_docs.py` 실행 후:
   - 생성된 API 문서에 `.NET 8.0` / `.NET 9.0` 미존재
3. csproj 버전을 임의로 변경해도 스크립트 재실행 시 문서에 자동 반영됨
4. GitHub Actions `docs.yml` 실행 성공 (빌드 오류 없음)
