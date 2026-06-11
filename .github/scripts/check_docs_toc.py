#!/usr/bin/env python3
"""
check_docs_toc.py
Docs/ 아래 toc.yml 등재 여부와 링크 유효성을 검사한다.

검사 1: Docs/ai-sdlc/*.md 중 ai-sdlc/toc.yml 미등재 파일
검사 2: Docs/operations/ 아래 ai-sdlc-*.md 잔존 파일 경고
검사 3: Docs/ai-sdlc/toc.yml의 href 대상 파일 존재 여부
검사 4: Docs/docfx.json metadata의 csproj 경로 존재 여부

exit code: 0 = 이상 없음, 1 = 경고 있음
docs.yml에서 `|| true`로 비차단 실행된다.
"""

import io
import json
import sys
from pathlib import Path

# Windows 터미널 UTF-8 출력 보장
if sys.stdout.encoding and sys.stdout.encoding.lower() not in ("utf-8", "utf8"):
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parent.parent.parent
DOCS = ROOT / "Docs"
AI_SDLC = DOCS / "ai-sdlc"
OPERATIONS = DOCS / "operations"
PLATFORMA = ROOT / "PlatformA"


def load_toc_hrefs(toc_path: Path) -> set[str]:
    """toc.yml에서 href 값을 수집한다."""
    hrefs = set()
    if not toc_path.exists():
        return hrefs
    for line in toc_path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if line.startswith("href:"):
            href = line.split(":", 1)[1].strip()
            hrefs.add(href)
    return hrefs


def check_ai_sdlc_toc() -> list[str]:
    """Docs/ai-sdlc/*.md 중 toc.yml 미등재 파일을 찾는다."""
    warnings = []
    toc_path = AI_SDLC / "toc.yml"
    registered = load_toc_hrefs(toc_path)

    for md in sorted(AI_SDLC.glob("*.md")):
        if md.name not in registered:
            warnings.append(f"[미등재] Docs/ai-sdlc/{md.name} — toc.yml에 없음")
    return warnings


def check_operations_residual() -> list[str]:
    """operations/ 아래 ai-sdlc-*.md 잔존 파일을 경고한다."""
    warnings = []
    for f in sorted(OPERATIONS.glob("ai-sdlc-*.md")):
        warnings.append(f"[잔존파일] {f.relative_to(ROOT)} — Docs/ai-sdlc/로 이동 권장")
    return warnings


def check_toc_href_targets(section_dir: Path) -> list[str]:
    """toc.yml의 href 대상 파일이 실제로 존재하는지 확인한다."""
    warnings = []
    toc_path = section_dir / "toc.yml"
    if not toc_path.exists():
        return [f"[누락] {section_dir.name}/toc.yml 없음"]

    for href in load_toc_hrefs(toc_path):
        if href.endswith("/") or href.startswith("http"):
            continue
        target = section_dir / href
        if not target.exists():
            warnings.append(f"[링크깨짐] {section_dir.name}/toc.yml → {href} 대상 없음")
    return warnings


def check_docfx_json_csproj() -> list[str]:
    """docfx.json metadata의 csproj 경로가 존재하는지 확인한다."""
    warnings = []
    docfx_path = DOCS / "docfx.json"
    if not docfx_path.exists():
        return ["[누락] Docs/docfx.json 없음"]

    try:
        data = json.loads(docfx_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as e:
        return [f"[파싱오류] Docs/docfx.json: {e}"]

    for meta in data.get("metadata", []):
        for src_entry in meta.get("src", []):
            # src 필드: Docs/ 기준 상대 경로 (예: "../PlatformA")
            base_dir = (DOCS / src_entry.get("src", ".")).resolve()
            for f in src_entry.get("files", []):
                if f.endswith(".csproj"):
                    csproj = (base_dir / f).resolve()
                    if not csproj.exists():
                        warnings.append(f"[csproj없음] docfx.json 등록된 {f} 파일 없음")
    return warnings


def main() -> int:
    all_warnings: list[str] = []

    print("=== check_docs_toc.py ===")

    w = check_ai_sdlc_toc()
    all_warnings.extend(w)

    w = check_operations_residual()
    all_warnings.extend(w)

    w = check_toc_href_targets(AI_SDLC)
    all_warnings.extend(w)

    w = check_docfx_json_csproj()
    all_warnings.extend(w)

    if all_warnings:
        print(f"\n경고 {len(all_warnings)}건:")
        for warn in all_warnings:
            print(f"  {warn}")
        return 1
    else:
        print("이상 없음 — 모든 검사 통과")
        return 0


if __name__ == "__main__":
    sys.exit(main())
