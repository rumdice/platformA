#!/usr/bin/env python3
"""
PostgreSQL sdlc.ai_model_runs + sdlc.ai_jobs 기반 cost-log markdown 생성.

사용법:
  python generate_cost_log_from_db.py --dry-run
  python generate_cost_log_from_db.py --output AI/reports/generated-cost-log-from-db.md

DB 연결 실패 시 graceful skip (exit 0).
현재 AI/cost-log.md를 대체하지 않음 (Phase C.2에서 전환).
"""

import argparse
import os
import sys
from datetime import datetime, timezone
from pathlib import Path

CONN_STR = os.environ.get(
    "SDLC_DB_CONNECTION",
    "Host=localhost;Port=5432;Database=platforma_sdlc;Username=platforma;Password=platforma_dev_password",
)


def parse_conn(conn_str: str) -> dict:
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


def get_conn():
    try:
        import psycopg2
        return psycopg2.connect(**parse_conn(CONN_STR))
    except ImportError:
        print("[generate_cost_log] psycopg2 미설치", file=sys.stderr)
        return None
    except Exception as e:
        print(f"[generate_cost_log] PostgreSQL 연결 실패: {e}", file=sys.stderr)
        return None


def fetch_runs(conn) -> list[dict]:
    cur = conn.cursor()
    cur.execute(
        """
        SELECT
            j.sprint,
            j.task_name,
            j.branch,
            j.status,
            j.pr_url,
            (SELECT r.model_name FROM sdlc.ai_model_runs r WHERE r.job_id = j.id ORDER BY r.created_at DESC LIMIT 1) AS model_name,
            j.duration_sec,
            j.consume_tokens,
            j.cache_tokens,
            j.created_at
        FROM sdlc.ai_jobs j
        WHERE j.status = 'done'
          AND j.consume_tokens IS NOT NULL
        ORDER BY j.sprint ASC, j.created_at ASC
        """
    )
    cols = ["sprint", "task_name", "branch", "status", "pr_url",
            "model_id", "duration_sec", "consume_tokens", "cache_tokens", "created_at"]
    return [dict(zip(cols, row)) for row in cur.fetchall()]


def size_label(file_count: int) -> str:
    if file_count <= 2:
        return "S"
    elif file_count <= 10:
        return "M"
    elif file_count <= 30:
        return "L"
    return "XL"


def fmt_int(v) -> str:
    if v is None:
        return "null"
    return f"{int(v):,}"


def fmt_sec(v) -> str:
    if v is None:
        return "null"
    return str(int(v))


def generate_markdown(runs: list[dict]) -> str:
    now = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
    total_duration = sum(r["duration_sec"] or 0 for r in runs)
    total_consume = sum(r["consume_tokens"] or 0 for r in runs)
    total_cache = sum(r["cache_tokens"] or 0 for r in runs)

    # By sprint aggregation
    by_sprint: dict[int, dict] = {}
    for r in runs:
        s = r["sprint"] or 0
        if s not in by_sprint:
            by_sprint[s] = {"runs": 0, "duration": 0, "consume": 0, "cache": 0}
        by_sprint[s]["runs"] += 1
        by_sprint[s]["duration"] += r["duration_sec"] or 0
        by_sprint[s]["consume"] += r["consume_tokens"] or 0
        by_sprint[s]["cache"] += r["cache_tokens"] or 0

    lines = [
        "# AI_SDLC Cost Log (DB 기반)",
        "",
        f"> 생성 시각: {now}",
        "> 소스: PostgreSQL `sdlc.ai_model_runs` JOIN `sdlc.ai_jobs`",
        "> 이 파일은 스크립트로 생성됩니다. 직접 편집하지 마세요.",
        "",
        "## Summary",
        "",
        "| Total Runs | Total Duration (sec) | Total Consume Tokens | Total Cache Tokens |",
        "|---:|---:|---:|---:|",
        f"| {len(runs)} | {fmt_sec(total_duration)} | {fmt_int(total_consume)} | {fmt_int(total_cache)} |",
        "",
        "## By Sprint",
        "",
        "| Sprint | Runs | Duration (sec) | Consume Tokens | Cache Tokens |",
        "|---:|---:|---:|---:|---:|",
    ]
    for sprint in sorted(by_sprint.keys()):
        d = by_sprint[sprint]
        lines.append(
            f"| #{sprint} | {d['runs']} | {fmt_sec(d['duration'])} | {fmt_int(d['consume'])} | {fmt_int(d['cache'])} |"
        )

    lines += [
        "",
        "## Details",
        "",
        "| Sprint | Task | Model | Duration (sec) | Consume Tokens | Cache Tokens | Date |",
        "|---:|---|---|---:|---:|---:|---|",
    ]
    for r in runs:
        date_str = r["created_at"].strftime("%Y-%m-%d") if r["created_at"] else ""
        model = r["model_id"] or "claude-sonnet-4-6"
        lines.append(
            f"| #{r['sprint']} | {r['task_name']} | {model} | {fmt_sec(r['duration_sec'])} | {fmt_int(r['consume_tokens'])} | {fmt_int(r['cache_tokens'])} | {date_str} |"
        )

    lines.append("")
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser(description="DB 기반 cost-log markdown 생성")
    parser.add_argument("--dry-run", action="store_true", help="stdout에만 출력 (파일 미저장)")
    parser.add_argument("--output", help="저장할 파일 경로")
    args = parser.parse_args()

    conn = get_conn()
    if conn is None:
        print("[generate_cost_log] DB 연결 실패 - 건너뜀", file=sys.stderr)
        sys.exit(0)

    try:
        runs = fetch_runs(conn)
        print(f"[generate_cost_log] {len(runs)}개 model run 조회", file=sys.stderr)

        md = generate_markdown(runs)

        if args.dry_run or not args.output:
            print(md)
        else:
            out_path = Path(args.output)
            out_path.parent.mkdir(parents=True, exist_ok=True)
            out_path.write_text(md, encoding="utf-8")
            print(f"[generate_cost_log] 저장 완료: {out_path}", file=sys.stderr)
    finally:
        conn.close()


if __name__ == "__main__":
    main()
