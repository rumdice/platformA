#!/usr/bin/env python3
"""
generate_doc_meta.py
csproj에서 .NET 버전을, 테스트 파일에서 테스트 수를 자동으로 계산하여
Docs/index.md 및 Docs/architecture/overview.md를 갱신합니다.

docs.yml의 docfx 빌드 직전 스텝으로 실행됩니다.
"""

import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent

# ─────────────────────────────────────────────
# 서비스 → csproj 매핑
# ─────────────────────────────────────────────
SERVICES = [
    ("Auth API", "PlatformA/PlatformA.Auth.API/PlatformA.Auth.API.csproj", ""),
    ("Ticketing API", "PlatformA/PlatformA.Ticketing.API/PlatformA.Ticketing.API.csproj", ""),
    ("Matching API", "PlatformA/PlatformA.Matching.API/PlatformA.Matching.API.csproj", ""),
    ("Game Server", "PlatformA/PlatformA.Game.Server/PlatformA.Game.Server.csproj", "Console App (ASP.NET Core 아님)"),
    ("Utils API", "PlatformA/PlatformA.Utils.API/PlatformA.Utils.API.csproj", ""),
]

TEST_DIRS = [
    "PlatformA/PlatformA.Tests.Auth.API",
    "PlatformA/PlatformA.Tests.Ticketing.API",
    "PlatformA/PlatformA.Tests.Matching.API",
    "PlatformA/PlatformA.Tests.Utils.API",
    "PlatformA/PlatformA.Tests.Game.Server",
]


def get_dotnet_version(csproj_relative: str, fallback: str = ".NET ?") -> str:
    """csproj XML에서 TargetFramework를 읽어 '.NET X.0' 형태로 반환."""
    try:
        tree = ET.parse(ROOT / csproj_relative)
        for elem in tree.iter():
            if elem.tag.endswith("TargetFramework") and elem.text:
                m = re.match(r"^net(\d+)\.(\d+)$", elem.text.strip())
                if m:
                    return f".NET {m.group(1)}.{m.group(2)}"
    except Exception as e:
        print(f"[warn] csproj 파싱 실패 ({csproj_relative}): {e}", file=sys.stderr)
    return fallback


def count_tests() -> int:
    """테스트 프로젝트 .cs 파일에서 [Fact] / [Theory] 어트리뷰트를 카운팅한다.
    라인 주석(// ...) 및 블록 주석(/* ... */) 내 발생은 제외한다."""
    total = 0
    for test_dir in TEST_DIRS:
        dir_path = ROOT / test_dir
        if not dir_path.exists():
            continue
        for cs_file in dir_path.rglob("*.cs"):
            try:
                content = cs_file.read_text(encoding="utf-8")
            except Exception:
                continue
            # 블록 주석 제거
            content = re.sub(r"/\*.*?\*/", "", content, flags=re.DOTALL)
            # 라인 주석 제거
            content = re.sub(r"//[^\n]*", "", content)
            # [Fact] 또는 [Theory] 카운팅 (대소문자 구분)
            total += len(re.findall(r"\[(?:Fact|Theory)\b", content))
    return total


def build_runtime_summary(versions: list[str]) -> str:
    """고유 버전 목록에서 'X.0 / Y.0' 형태의 요약 문자열 생성."""
    unique = sorted(set(versions), key=lambda v: [int(x) for x in re.findall(r"\d+", v)])
    return " / ".join(unique)


def update_marker(content: str, marker: str, new_value: str) -> str:
    """<!-- MARKER --> ... <!-- /MARKER --> 사이 내용을 교체한다."""
    pattern = rf"<!--\s*{re.escape(marker)}\s*-->.*?<!--\s*/{re.escape(marker)}\s*-->"
    replacement = f"<!-- {marker} -->{new_value}<!-- /{marker} -->"
    updated = re.sub(pattern, replacement, content, flags=re.DOTALL)
    if updated == content:
        print(f"[warn] 마커 '{marker}'를 찾지 못해 교체를 건너뜁니다.", file=sys.stderr)
    return updated


def update_index_md(runtime_summary: str, test_count: int) -> None:
    """Docs/index.md의 RUNTIME_VERSION / TEST_COUNT 마커 교체."""
    index_path = ROOT / "Docs/index.md"
    if not index_path.exists():
        print(f"[skip] {index_path} 없음", file=sys.stderr)
        return

    content = index_path.read_text(encoding="utf-8")
    content = update_marker(content, "RUNTIME_VERSION", runtime_summary)
    content = update_marker(content, "TEST_COUNT", f"{test_count}개")
    index_path.write_text(content, encoding="utf-8")
    print(f"[ok] Docs/index.md 갱신 - 런타임: {runtime_summary}, 테스트: {test_count}개")


def build_runtime_table(service_versions: list[tuple[str, str, str]]) -> str:
    """런타임 버전 마크다운 테이블 생성."""
    lines = [
        "| 서비스 | .NET 버전 | 비고 |",
        "|--------|----------|------|",
    ]
    for name, version, remark in service_versions:
        lines.append(f"| {name} | {version} | {remark} |")
    return "\n".join(lines)


def update_overview_md(service_versions: list[tuple[str, str, str]]) -> None:
    """Docs/architecture/overview.md의 RUNTIME_TABLE 마커 교체."""
    overview_path = ROOT / "Docs/architecture/overview.md"
    if not overview_path.exists():
        print(f"[skip] {overview_path} 없음", file=sys.stderr)
        return

    table = build_runtime_table(service_versions)
    content = overview_path.read_text(encoding="utf-8")
    content = update_marker(content, "RUNTIME_TABLE", f"\n{table}\n")
    overview_path.write_text(content, encoding="utf-8")
    print(f"[ok] Docs/architecture/overview.md 갱신 - {len(service_versions)}개 서비스 버전 반영")


def main() -> None:
    print("=== generate_doc_meta.py 시작 ===")

    # 1. 각 서비스 .NET 버전 수집
    service_versions: list[tuple[str, str, str]] = []
    all_versions: list[str] = []
    for name, csproj, remark in SERVICES:
        version = get_dotnet_version(csproj)
        service_versions.append((name, version, remark))
        all_versions.append(version)
        print(f"  {name}: {version}")

    # 2. 테스트 수 계산
    test_count = count_tests()
    print(f"  테스트 수: {test_count}개")

    # 3. 런타임 요약 (고유 버전 조합)
    runtime_summary = build_runtime_summary(all_versions)
    print(f"  런타임 요약: {runtime_summary}")

    # 4. 문서 갱신
    update_index_md(runtime_summary, test_count)
    update_overview_md(service_versions)

    print("=== generate_doc_meta.py 완료 ===")


if __name__ == "__main__":
    main()
