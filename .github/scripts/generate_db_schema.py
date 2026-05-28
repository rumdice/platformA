#!/usr/bin/env python3
"""
generate_db_schema.py
MySqlDB.Lib Entity 클래스에서 Docs/architecture/database-schema.md 의
"## 테이블 명세" 섹션을 자동 갱신합니다.
ER 다이어그램·Migration 섹션은 보존됩니다.
"""

import re
import sys
from pathlib import Path
from typing import Optional

ROOT = Path(__file__).resolve().parent.parent.parent

ENTITY_DIRS = [
    "PlatformA/PlatformA.MySqlDB.Lib/DBWebApp/Entities",
    "PlatformA/PlatformA.MySqlDB.Lib/DBLogApp/Entities",
]

OUTPUT = ROOT / "Docs/architecture/database-schema.md"

# C# 타입 → SQL 타입 대응 (대략적)
CSHARP_TO_SQL = {
    "int": "INT",
    "long": "BIGINT",
    "string": "VARCHAR",
    "bool": "TINYINT(1)",
    "DateTime": "DATETIME(6)",
    "byte": "TINYINT",
    "short": "SMALLINT",
    "decimal": "DECIMAL",
    "float": "FLOAT",
    "double": "DOUBLE",
}


def to_snake_case(name: str) -> str:
    """PascalCase → snake_case."""
    s1 = re.sub(r'(.)([A-Z][a-z]+)', r'\1_\2', name)
    return re.sub(r'([a-z0-9])([A-Z])', r'\1_\2', s1).lower()


def parse_entity(filepath: Path) -> Optional[dict]:
    """단일 Entity .cs 파일을 파싱하여 테이블 정보 반환."""
    src = filepath.read_text(encoding="utf-8", errors="ignore")

    # enum은 건너뜀
    if re.search(r'public enum \w+', src) and not re.search(r'public class \w+', src):
        return None

    # 클래스 이름
    cls_m = re.search(r'public class (\w+)\b', src)
    if not cls_m:
        return None
    class_name = cls_m.group(1)
    if class_name.endswith("Context") or class_name.endswith("Factory"):
        return None

    # 클래스 summary (테이블명 포함 여부)
    summary = ""
    sm = re.search(r'/// <summary>([\s\S]*?)/// </summary>', src)
    if sm:
        summary = " ".join(
            re.sub(r'<[^>]+>', '', line.strip().lstrip("/ ")).strip()
            for line in sm.group(1).splitlines()
            if line.strip().startswith("///")
        )

    # 테이블명 추론 (summary의 "테이블: xxx" 패턴 또는 snake_case 변환)
    table_m = re.search(r'테이블:\s*(\S+)', summary)
    table_name = table_m.group(1) if table_m else to_snake_case(class_name) + "s"

    # 클래스 바디 추출
    brace_pos = src.find("{", cls_m.end())
    if brace_pos == -1:
        return None
    depth = 0
    body_end = brace_pos
    for i in range(brace_pos, len(src)):
        if src[i] == "{":
            depth += 1
        elif src[i] == "}":
            depth -= 1
            if depth == 0:
                body_end = i
                break
    body = src[brace_pos : body_end + 1]

    # 속성 파싱
    prop_re = re.compile(
        r'((?:\[[^\]]+\]\s*)*)'                    # 어트리뷰트
        r'public\s+([\w<>?\[\]]+)\s+(\w+)\s*\{[^}]*get[^}]*\}',
        re.DOTALL,
    )

    columns = []
    for pm in prop_re.finditer(body):
        attrs = pm.group(1)
        raw_type = pm.group(2)
        prop_name = pm.group(3)

        # Navigation property 건너뜀 (ICollection, 다른 Entity 타입)
        if re.match(r'ICollection|IEnumerable', raw_type):
            continue

        # nullable 제거
        nullable = raw_type.endswith("?")
        base_type = raw_type.rstrip("?")

        # 인라인 주석에서 컬럼명 추출 (// col_name TYPE ...)
        inline_m = re.search(rf'public\s+{re.escape(raw_type)}\s+{prop_name}\s*{{[^}}]*}}\s*//\s*(\w+)', body)
        col_comment = inline_m.group(1) if inline_m else to_snake_case(prop_name)

        sql_type = CSHARP_TO_SQL.get(base_type, base_type.upper())

        # 제약 추론
        constraints = []
        max_len_m = re.search(r'\[MaxLength\((\d+)', attrs)
        if sql_type == "VARCHAR":
            length = max_len_m.group(1) if max_len_m else "255"
            sql_type = f"VARCHAR({length})"

        if "Key" in attrs or prop_name == "Id" or prop_name.endswith("Id") and "PK" in (inline_m.group(0) if inline_m else ""):
            constraints.append("PK")
        if not nullable and base_type != "DateTime":
            constraints.append("NOT NULL")
        if "Required" in attrs:
            constraints.append("NOT NULL")

        # 인라인 주석 전체 (설명용)
        full_inline_m = re.search(
            rf'public\s+{re.escape(raw_type)}\s+{prop_name}\s*{{[^}}]*}}\s*//\s*(.+)',
            body
        )
        description = full_inline_m.group(1).strip() if full_inline_m else ""

        columns.append({
            "col": col_comment,
            "type": sql_type,
            "constraints": ", ".join(constraints) if constraints else "—",
            "desc": description,
        })

    if not columns:
        return None

    return {
        "class_name": class_name,
        "table_name": table_name,
        "summary": summary,
        "columns": columns,
    }


def render_table_section(entities: list[dict]) -> str:
    """엔티티 목록 → '## 테이블 명세' 마크다운 섹션."""
    lines = ["## 테이블 명세", ""]
    lines.append("> **이 섹션은 `.github/scripts/generate_db_schema.py`로 자동 갱신됩니다.**")
    lines.append("")

    for ent in entities:
        lines.append(f"### {ent['table_name']}")
        lines.append("")
        if ent["summary"]:
            lines.append(f"> {ent['summary']}")
            lines.append("")
        lines.append("| 컬럼 | 타입 | 제약 | 설명 |")
        lines.append("|------|------|------|------|")
        for col in ent["columns"]:
            lines.append(f"| `{col['col']}` | {col['type']} | {col['constraints']} | {col['desc']} |")
        lines.append("")
        lines.append("---")
        lines.append("")

    return "\n".join(lines)


def update_schema_file(new_section: str) -> None:
    """database-schema.md 의 '## 테이블 명세' 섹션만 교체한다."""
    if not OUTPUT.exists():
        OUTPUT.parent.mkdir(parents=True, exist_ok=True)
        OUTPUT.write_text(new_section, encoding="utf-8")
        return

    original = OUTPUT.read_text(encoding="utf-8")

    # "## 테이블 명세" 섹션 탐색 (시작 ~ 다음 ## 또는 파일 끝)
    section_re = re.compile(r'^## 테이블 명세\b.*?(?=^## |\Z)', re.MULTILINE | re.DOTALL)
    if section_re.search(original):
        updated = section_re.sub(new_section, original)
    else:
        # 섹션이 없으면 파일 끝에 추가
        updated = original.rstrip() + "\n\n" + new_section

    OUTPUT.write_text(updated, encoding="utf-8")


def main():
    entities = []
    for entity_dir in ENTITY_DIRS:
        dir_path = ROOT / entity_dir
        if not dir_path.exists():
            print(f"[WARN] 디렉터리 없음: {entity_dir}")
            continue
        for cs_file in sorted(dir_path.glob("*.cs")):
            result = parse_entity(cs_file)
            if result:
                entities.append(result)
                print(f"  [파싱] {cs_file.name} → {result['table_name']}")

    if not entities:
        print("[WARN] 파싱된 엔티티가 없습니다.")
        return 1

    section = render_table_section(entities)
    update_schema_file(section)
    print(f"[OK] {OUTPUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
