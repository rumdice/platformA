#!/usr/bin/env python3
"""
generate_redis_key_docs.py
Consts.cs의 Redis 키 상수를 파싱하여 Docs/architecture/redis-keyspace.md의
마커 구간(REDIS_KEY_TABLE)을 자동 갱신합니다.
docs.yml의 docfx 빌드 직전 스텝으로 실행됩니다.
"""

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
CONSTS_PATH = ROOT / "PlatformA/PlatformA.Library/Common/Consts.cs"
OUTPUT_PATH = ROOT / "Docs/architecture/redis-keyspace.md"

# 서비스 이름 추론: 클래스 내 region 또는 주석 블록 이름
# 더 구체적인 패턴을 먼저 배치 (MATCH_QUEUE가 QUEUE보다 우선 매칭되도록)
SERVICE_HINTS = {
    "REFRESH": "Auth API",
    "ACTIVE_USER": "Ticketing API",
    "TICKET": "Ticketing API",
    "MATCH": "Matching API",
    "ROOM": "Matching API",
    "QUEUE": "Ticketing API",
    "SHORT_URL": "Utils API",
    "DIRTY": "Utils API",
    "PLAYER": "Game Server",
}


def update_marker(content: str, marker: str, new_body: str) -> str:
    pattern = rf"(<!-- {marker}_START -->).*?(<!-- {marker}_END -->)"
    replacement = rf"\1\n{new_body}\n\2"
    updated, count = re.subn(pattern, replacement, content, flags=re.DOTALL)
    if count == 0:
        print(f"[warn] 마커 {marker}_START/END를 찾지 못했습니다 — 교체 생략")
    return updated


def infer_service(const_name: str) -> str:
    upper = const_name.upper()
    for hint, service in SERVICE_HINTS.items():
        if hint in upper:
            return service
    return "—"


def parse_ttl_seconds(src: str) -> dict[str, int]:
    """TTL 관련 상수를 초 단위로 수집."""
    ttl_map: dict[str, int] = {}
    for m in re.finditer(
        r"public\s+const\s+int\s+(\w+(?:TTL_SECONDS|EXPIRY_SECONDS|EXPIRY_MINUTES|EXPIRY_DAYS|TIMEOUT_SECONDS))\s*=\s*(\d+)",
        src,
    ):
        name = m.group(1)
        val = int(m.group(2))
        if "DAYS" in name:
            val *= 86400
        elif "MINUTES" in name:
            val *= 60
        ttl_map[name] = val
    return ttl_map


def seconds_to_human(seconds: int) -> str:
    if seconds >= 86400:
        return f"{seconds // 86400}일 ({seconds:,}초)"
    if seconds >= 3600:
        return f"{seconds // 3600}시간 ({seconds:,}초)"
    if seconds >= 60:
        return f"{seconds // 60}분 ({seconds:,}초)"
    return f"{seconds}초"


def parse_redis_keys(src: str, ttl_map: dict[str, int]) -> list[dict]:
    keys = []
    lines = src.splitlines()

    for i, line in enumerate(lines):
        # KEY 또는 KEY_PREFIX 상수 매칭
        m = re.match(
            r'\s*public\s+const\s+string\s+(\w+(?:_KEY|_KEY_PREFIX))\s*=\s*"([^"]+)"',
            line,
        )
        if not m:
            continue

        const_name = m.group(1)
        key_pattern = m.group(2)

        # PREFIX 패턴은 * 붙여서 표시
        if const_name.endswith("_PREFIX"):
            key_pattern = key_pattern + "{id}"

        # 윗줄 주석 추출
        comment = ""
        if i > 0:
            prev = lines[i - 1].strip()
            if prev.startswith("//"):
                comment = prev.lstrip("/ ").strip()
            elif i > 1:
                prev2 = lines[i - 2].strip()
                if prev2.startswith("//"):
                    comment = prev2.lstrip("/ ").strip()

        # TTL 연결: 상수명에서 prefix 부분으로 TTL 매칭
        prefix = re.sub(r"_KEY(?:_PREFIX)?$", "", const_name)
        ttl_sec = None
        for ttl_name, ttl_val in ttl_map.items():
            if prefix in ttl_name or ttl_name.startswith(prefix):
                ttl_sec = ttl_val
                break

        ttl_str = seconds_to_human(ttl_sec) if ttl_sec else "없음"
        service = infer_service(const_name)

        keys.append({
            "const_name": const_name,
            "key_pattern": key_pattern,
            "comment": comment,
            "ttl": ttl_str,
            "service": service,
        })

    return keys


def render_key_table(keys: list[dict]) -> str:
    lines = [
        "| 상수명 | 키 패턴 | TTL | 서비스 | 설명 |",
        "|---|---|---|---|---|",
    ]
    for k in keys:
        lines.append(
            f"| `{k['const_name']}` | `{k['key_pattern']}` | {k['ttl']} | {k['service']} | {k['comment']} |"
        )
    return "\n".join(lines)


def main() -> None:
    if not CONSTS_PATH.exists():
        print(f"[error] Consts.cs를 찾을 수 없습니다: {CONSTS_PATH}")
        sys.exit(1)
    if not OUTPUT_PATH.exists():
        print(f"[error] 출력 문서를 찾을 수 없습니다: {OUTPUT_PATH}")
        sys.exit(1)

    src = CONSTS_PATH.read_text(encoding="utf-8")
    ttl_map = parse_ttl_seconds(src)
    keys = parse_redis_keys(src, ttl_map)

    content = OUTPUT_PATH.read_text(encoding="utf-8")
    content = update_marker(content, "REDIS_KEY_TABLE", render_key_table(keys))
    OUTPUT_PATH.write_text(content, encoding="utf-8")

    print(f"[ok] {OUTPUT_PATH.relative_to(ROOT)} 갱신 완료")
    print(f"     키 상수: {len(keys)}개, TTL 상수: {len(ttl_map)}개")


if __name__ == "__main__":
    main()
