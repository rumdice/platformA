#!/usr/bin/env python3
"""
generate_proto_docs.py
packets.proto를 파싱하여 Docs/developer-guide/packet-protocol.md의
마커 구간(PACKET_LIST, ENUM_LIST)을 자동 갱신합니다.
docs.yml의 docfx 빌드 직전 스텝으로 실행됩니다.
"""

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
PROTO_PATH = ROOT / "PlatformA/PlatformA.Library/Packets/Proto/packets.proto"
OUTPUT_PATH = ROOT / "Docs/developer-guide/packet-protocol.md"


def update_marker(content: str, marker: str, new_body: str) -> str:
    pattern = rf"(<!-- {marker}_START -->).*?(<!-- {marker}_END -->)"
    replacement = rf"\1\n{new_body}\n\2"
    updated, count = re.subn(pattern, replacement, content, flags=re.DOTALL)
    if count == 0:
        print(f"[warn] 마커 {marker}_START/END를 찾지 못했습니다 — 교체 생략")
    return updated


def parse_enums(src: str) -> list[dict]:
    enums = []
    for m in re.finditer(r"enum\s+(\w+)\s*\{([^}]+)\}", src, re.DOTALL):
        name = m.group(1)
        body = m.group(2)
        values = []
        for v in re.finditer(r"(\w+)\s*=\s*(\d+)\s*;", body):
            values.append({"name": v.group(1), "value": int(v.group(2))})
        enums.append({"name": name, "values": values})
    return enums


def parse_messages(src: str) -> list[dict]:
    messages = []
    # message 선언 윗줄 주석 추출용
    lines = src.splitlines()
    line_map: dict[int, str] = {}
    for i, line in enumerate(lines):
        m = re.match(r"\s*message\s+(\w+)", line)
        if m:
            comment = ""
            if i > 0:
                prev = lines[i - 1].strip()
                if prev.startswith("//"):
                    comment = prev.lstrip("/ ").strip()
            line_map[m.group(1)] = comment

    for m in re.finditer(r"message\s+(\w+)\s*\{([^}]+)\}", src, re.DOTALL):
        name = m.group(1)
        if name == "Packet":
            continue
        body = m.group(2)
        fields = []
        for f in re.finditer(r"(\w+)\s+(\w+)\s*=\s*(\d+)\s*;", body):
            fields.append({
                "type": f.group(1),
                "name": f.group(2),
                "tag": int(f.group(3)),
            })
        messages.append({
            "name": name,
            "comment": line_map.get(name, ""),
            "fields": fields,
        })
    return messages


def parse_oneof(src: str) -> list[dict]:
    m = re.search(r"message\s+Packet\s*\{[^}]*oneof\s+\w+\s*\{([^}]+)\}", src, re.DOTALL)
    if not m:
        return []
    entries = []
    for e in re.finditer(r"(\w+)\s+(\w+)\s*=\s*(\d+)\s*;", m.group(1)):
        entries.append({
            "msg_type": e.group(1),
            "field_name": e.group(2),
            "tag": int(e.group(3)),
        })
    return entries


def render_packet_list(messages: list[dict], oneof: list[dict]) -> str:
    tag_map = {e["msg_type"]: e["tag"] for e in oneof}

    c_msgs = [msg for msg in messages if msg["name"].startswith("C")]
    s_msgs = [msg for msg in messages if msg["name"].startswith("S")]

    def field_str(fields: list[dict]) -> str:
        return ", ".join(f"`{f['type']} {f['name']}`" for f in fields) or "—"

    def comment_str(comment: str) -> str:
        # "Client → Server: 이동 요청" 에서 설명만 추출
        if ":" in comment:
            return comment.split(":", 1)[1].strip()
        return comment

    lines = ["### 클라이언트 → 서버 (CXxx)", ""]
    lines.append("| 패킷 | oneof tag | 필드 | 설명 |")
    lines.append("|---|---|---|---|")
    for msg in c_msgs:
        tag = tag_map.get(msg["name"], "—")
        lines.append(f"| `{msg['name']}` | {tag} | {field_str(msg['fields'])} | {comment_str(msg['comment'])} |")

    lines += ["", "### 서버 → 클라이언트 (SXxx)", ""]
    lines.append("| 패킷 | oneof tag | 필드 | 설명 |")
    lines.append("|---|---|---|---|")
    for msg in s_msgs:
        tag = tag_map.get(msg["name"], "—")
        lines.append(f"| `{msg['name']}` | {tag} | {field_str(msg['fields'])} | {comment_str(msg['comment'])} |")

    return "\n".join(lines)


def render_enum_list(enums: list[dict]) -> str:
    lines = ["### 결과 코드 열거형", ""]
    for enum in enums:
        lines.append(f"**{enum['name']}**")
        lines.append("")
        lines.append("| 값 | 이름 | 설명 |")
        lines.append("|---|---|---|")
        for v in enum["values"]:
            note = "(proto3 기본값 — wire에 포함되지 않음)" if v["value"] == 0 else ""
            lines.append(f"| {v['value']} | `{v['name']}` | {note} |")
        lines.append("")
    return "\n".join(lines).rstrip()


def main() -> None:
    if not PROTO_PATH.exists():
        print(f"[error] proto 파일을 찾을 수 없습니다: {PROTO_PATH}")
        sys.exit(1)
    if not OUTPUT_PATH.exists():
        print(f"[error] 출력 문서를 찾을 수 없습니다: {OUTPUT_PATH}")
        sys.exit(1)

    src = PROTO_PATH.read_text(encoding="utf-8")
    enums = parse_enums(src)
    messages = parse_messages(src)
    oneof = parse_oneof(src)

    content = OUTPUT_PATH.read_text(encoding="utf-8")
    content = update_marker(content, "PACKET_LIST", render_packet_list(messages, oneof))
    content = update_marker(content, "ENUM_LIST", render_enum_list(enums))
    OUTPUT_PATH.write_text(content, encoding="utf-8")

    print(f"[ok] {OUTPUT_PATH.relative_to(ROOT)} 갱신 완료")
    print(f"     메시지: {len(messages)}개, 열거형: {len(enums)}개")


if __name__ == "__main__":
    main()
