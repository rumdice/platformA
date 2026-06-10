#!/usr/bin/env python3
"""
AI/SPRINT.md의 Active Sprint 및 Recent Sprints 테이블을 자동 재생성한다.

데이터 소스 우선순위:
  1. PostgreSQL DB (SDLC_DB_CONNECTION 환경변수) — 완전한 상태 정보
  2. AI/sprints/sprint-*.md 파일 YAML 프론트매터 — DB 없을 때 폴백

호출 시점:
  - pr-merge-sync.yml (GitHub Actions, PR 머지 후 자동)
  - 로컬 수동: python .github/scripts/generate_sprint_md.py

DB 연결 실패 시: 파일 기반 폴백, exit 0 (graceful)
에이전트는 AI/SPRINT.md를 직접 수정하지 않는다 (동시 개발 충돌 방지).
"""

import os
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
SPRINT_MD = REPO_ROOT / "AI" / "SPRINT.md"
SPRINTS_DIR = REPO_ROOT / "AI" / "sprints"

CONN_STR = os.environ.get(
    "SDLC_DB_CONNECTION",
    "Host=localhost;Port=5432;Database=platforma_sdlc;Username=platforma;Password=platforma_dev_password",
)

RECENT_LIMIT = 15
ACTIVE_LIMIT = 10


# ---------------------------------------------------------------------------
# DB 연결
# ---------------------------------------------------------------------------

def _parse_conn(conn_str: str) -> dict:
    parts = {}
    for part in conn_str.split(";"):
        if "=" in part:
            k, v = part.split("=", 1)
            parts[k.strip().lower()] = v.strip()
    return {
        "host": parts.get("host", "localhost"),
        "port": int(parts.get("port", 5432)),
        "dbname": parts.get("database", "platforma_sdlc"),
        "user": parts.get("username", "platforma"),
        "password": parts.get("password", "platforma_dev_password"),
    }


def get_db_conn():
    try:
        import psycopg2
        return psycopg2.connect(**_parse_conn(CONN_STR))
    except Exception:
        return None


# ---------------------------------------------------------------------------
# Sprint 파일 헬퍼
# ---------------------------------------------------------------------------

def parse_frontmatter(path: Path) -> dict:
    """YAML 프론트매터 파싱 (---...--- 블록). 없으면 빈 dict."""
    try:
        content = path.read_text(encoding="utf-8")
        if not content.startswith("---"):
            return {}
        end = content.index("---", 3)
        block = content[3:end]
        data = {}
        for line in block.splitlines():
            if ":" in line:
                k, _, v = line.partition(":")
                data[k.strip()] = v.strip()
        return data
    except Exception:
        return {}


def sprint_num_from_filename(path: Path) -> int:
    m = re.search(r"sprint-(\d+)", path.stem)
    return int(m.group(1)) if m else 0


def get_sprint_title(sprint_num: int, task_name: str) -> str:
    """
    제목 해석 우선순위:
      1. sprint-NNN.md 프론트매터 title
      2. sprint-NNN.md 첫 # 헤딩에서 추출 (구형 파일)
      3. task_name 또는 "Sprint #NNN"
    """
    p = SPRINTS_DIR / f"sprint-{sprint_num:03d}.md"
    if not p.exists():
        return task_name or f"Sprint #{sprint_num}"
    fm = parse_frontmatter(p)
    if fm.get("title"):
        return fm["title"]
    try:
        for line in p.read_text(encoding="utf-8").splitlines():
            if line.startswith("# "):
                m = re.match(r"#\s+Sprint #\d+\s+[—\-]\s+(.*)", line)
                return m.group(1).strip() if m else line[2:].strip()
    except Exception:
        pass
    return task_name or f"Sprint #{sprint_num}"


def sprint_file_link(sprint_num: int) -> str:
    p = SPRINTS_DIR / f"sprint-{sprint_num:03d}.md"
    if p.exists():
        return f"[`AI/sprints/sprint-{sprint_num:03d}.md`](sprints/sprint-{sprint_num:03d}.md)"
    return "—"


# ---------------------------------------------------------------------------
# DB 조회
# ---------------------------------------------------------------------------

def db_get_active(conn) -> list:
    """[(sprint, branch, task_name, status, owner)]  status != done"""
    try:
        cur = conn.cursor()
        cur.execute(
            """
            SELECT sprint, branch, task_name, status, owner
            FROM sdlc.ai_jobs
            WHERE status != 'done'
            ORDER BY sprint DESC NULLS LAST
            LIMIT %s
            """,
            (ACTIVE_LIMIT,),
        )
        return cur.fetchall()
    except Exception as e:
        print(f"[generate_sprint_md] db_get_active 실패: {e}", file=sys.stderr)
        return []


def db_get_recent_done(conn) -> list:
    """[(sprint, task_name, completed_at)]  status = done, DESC"""
    try:
        cur = conn.cursor()
        cur.execute(
            """
            SELECT sprint, task_name, completed_at
            FROM sdlc.ai_jobs
            WHERE status = 'done'
            ORDER BY sprint DESC NULLS LAST
            LIMIT %s
            """,
            (RECENT_LIMIT,),
        )
        return cur.fetchall()
    except Exception as e:
        print(f"[generate_sprint_md] db_get_recent_done 실패: {e}", file=sys.stderr)
        return []


# ---------------------------------------------------------------------------
# 파일 기반 폴백
# ---------------------------------------------------------------------------

def file_get_recent_done() -> list:
    """AI/sprints/sprint-*.md 스캔 → status=done → [(sprint, task_name, completed)]"""
    rows = []
    if not SPRINTS_DIR.exists():
        return rows
    for p in sorted(SPRINTS_DIR.glob("sprint-*.md"), reverse=True):
        fm = parse_frontmatter(p)
        if fm.get("status") == "done":
            num = sprint_num_from_filename(p)
            rows.append((num, fm.get("title", ""), fm.get("completed", fm.get("date", ""))))
        if len(rows) >= RECENT_LIMIT:
            break
    return rows


# ---------------------------------------------------------------------------
# 테이블 빌더
# ---------------------------------------------------------------------------

def build_active_table(rows: list) -> str:
    """5-column Active Sprint 마크다운 테이블."""
    header = (
        "| Sprint | 제목 | 상태 | Owner | 상세 파일 |\n"
        "|--------|------|------|-------|----------|\n"
    )
    if not rows:
        return header + "| — | (진행 중인 작업 없음) | — | — | — |\n"
    data_lines = []
    for sprint, branch, task_name, status, owner in rows:
        title = get_sprint_title(sprint, task_name)
        link = sprint_file_link(sprint)
        owner_str = owner or "—"
        data_lines.append(f"| #{sprint} | {title} | {status} | {owner_str} | {link} |")
    return header + "\n".join(data_lines) + "\n"


def build_recent_table(rows: list, source: str) -> str:
    """3-column Recent Sprints 마크다운 테이블."""
    header = (
        "| Sprint | 제목 | 완료일 |\n"
        "|--------|------|--------|\n"
    )
    if not rows:
        return header + "| — | (완료된 스프린트 없음) | — |\n"
    data_lines = []
    if source == "db":
        for sprint, task_name, completed_at in rows:
            title = get_sprint_title(sprint, task_name)
            if completed_at:
                date_str = (
                    completed_at.strftime("%Y-%m-%d")
                    if hasattr(completed_at, "strftime")
                    else str(completed_at)[:10]
                )
            else:
                date_str = "—"
            data_lines.append(f"| #{sprint} | {title} | {date_str} |")
    else:
        for sprint, title, completed in rows:
            title_str = title or get_sprint_title(sprint, "")
            data_lines.append(f"| #{sprint} | {title_str} | {completed or '—'} |")
    return header + "\n".join(data_lines) + "\n"


# ---------------------------------------------------------------------------
# SPRINT.md 섹션 교체
# ---------------------------------------------------------------------------

def replace_sprint_md(active_table: str, recent_table: str) -> bool:
    if not SPRINT_MD.exists():
        print(f"[generate_sprint_md] {SPRINT_MD} not found", file=sys.stderr)
        return False

    content = SPRINT_MD.read_text(encoding="utf-8").replace("\r\n", "\n").replace("\r", "\n")

    # Active Sprint 섹션: "## Active Sprint\n\n" + 테이블 행들
    active_pat = re.compile(r"(## Active Sprint\n\n)((?:\|[^\n]*\n)+)")
    # Recent Sprints 섹션: "## Recent Sprints (완료)\n\n" + 테이블 행들
    recent_pat = re.compile(r"(## Recent Sprints \(완료\)\n\n)((?:\|[^\n]*\n)+)")

    new_content = active_pat.sub(lambda m: m.group(1) + active_table, content, count=1)
    new_content = recent_pat.sub(lambda m: m.group(1) + recent_table, new_content, count=1)

    if new_content == content:
        print("[generate_sprint_md] SPRINT.md 변경 없음 (이미 최신)")
        return False

    SPRINT_MD.write_text(new_content, encoding="utf-8")
    print("[generate_sprint_md] SPRINT.md 갱신 완료")
    return True


# ---------------------------------------------------------------------------
# 메인
# ---------------------------------------------------------------------------

def main() -> None:
    conn = get_db_conn()

    if conn:
        print("[generate_sprint_md] DB 연결 성공 - DB 기반 재생성")
        try:
            active_rows = db_get_active(conn)
            recent_rows = db_get_recent_done(conn)
            recent_source = "db"
        finally:
            conn.close()
    else:
        print("[generate_sprint_md] DB 없음 — 파일 기반 폴백", file=sys.stderr)
        active_rows = []
        recent_rows = file_get_recent_done()
        recent_source = "file"

    active_table = build_active_table(active_rows)
    recent_table = build_recent_table(recent_rows, recent_source)

    replace_sprint_md(active_table, recent_table)


if __name__ == "__main__":
    main()
