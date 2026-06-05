#!/usr/bin/env python3
"""
AI/tasks/*.json → PlatformA.SdlcDB.Lib (PostgreSQL sdlc 스키마) 마이그레이션 스크립트.

사용법:
  python .github/scripts/migrate_tasks_to_postgres.py            # dry-run
  python .github/scripts/migrate_tasks_to_postgres.py --dry-run  # dry-run (명시)
  python .github/scripts/migrate_tasks_to_postgres.py --apply    # 실제 DB 이전
"""

import argparse
import json
import os
import sys
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
TASKS_DIR = REPO_ROOT / "AI" / "tasks"

CONN = os.environ.get(
    "SDLC_DB_CONNECTION",
    "Host=localhost;Port=5432;Database=platforma_sdlc;Username=platforma;Password=platforma_dev_password",
)

VALID_STEP_NAMES = {
    "plan", "requirement", "impact", "start", "test_gen",
    "done", "review", "pr", "gate_check", "merge_sync",
    "ci_failure", "qa_failure", "doc_sync", "pr_closed_unmerged",
}


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
        return psycopg2.connect(**parse_conn(CONN))
    except ImportError:
        print("[migrate] psycopg2 미설치. pip install psycopg2-binary", file=sys.stderr)
        return None
    except Exception as e:
        print(f"[migrate] PostgreSQL 연결 실패: {e}", file=sys.stderr)
        return None


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="AI/tasks JSON → PostgreSQL 마이그레이션")
    parser.add_argument("--dry-run", action="store_true", default=False,
                        help="DB 연결 없이 매핑 가능 여부만 검증")
    parser.add_argument("--apply", action="store_true",
                        help="PostgreSQL sdlc 스키마에 실제 upsert")
    return parser.parse_args()


def load_task_files() -> list[tuple[Path, dict]]:
    files = sorted(TASKS_DIR.glob("sprint*.json"))
    results = []
    for f in files:
        if f.name == "SCHEMA.md":
            continue
        try:
            data = json.loads(f.read_text(encoding="utf-8"))
            results.append((f, data))
        except json.JSONDecodeError as e:
            print(f"  [BROKEN] {f.name}: {e}", file=sys.stderr)
    return results


def map_to_ai_job(data: dict) -> dict:
    return {
        "sprint": data.get("sprint"),
        "task_name": data.get("task", ""),
        "branch": data.get("branch", ""),
        "status": data.get("status", ""),
        "pr_url": data.get("pr_url"),
        "test_generated": data.get("test_generated", False),
        "review_completed": data.get("review_completed", False),
        "adr_required": data.get("adr_required", False),
        "duration_sec": data.get("duration_sec"),
        "consume_tokens": data.get("consume_tokens"),
        "cache_tokens": data.get("cache_tokens"),
        "retry_count": data.get("retry_count", 0),
        "last_error": data.get("last_error"),
        "source_json": json.dumps(data),
        "impact": json.dumps(data.get("impact")) if data.get("impact") else None,
        "created_at": data.get("created_at"),
        "completed_at": data.get("completed_at"),
    }


def map_to_ai_job_steps(data: dict) -> list[dict]:
    steps = []
    for step in data.get("steps", []):
        steps.append({
            "step_name": step.get("name", ""),
            "status": step.get("status", ""),
            "summary": step.get("summary"),
            "started_at": step.get("started_at"),
            "completed_at": step.get("completed_at"),
            "result_json": None,
        })
    return steps


def run_dry_run(records: list[tuple[Path, dict]]) -> None:
    status_counts: dict[str, int] = defaultdict(int)
    step_counts: dict[str, int] = defaultdict(int)
    warnings: list[str] = []

    total_jobs = 0
    total_steps = 0
    broken = 0

    for path, data in records:
        job = map_to_ai_job(data)

        if not job["task_name"]:
            warnings.append(f"{path.name}: task_name missing")
        if not job["branch"]:
            warnings.append(f"{path.name}: branch missing")
        if not job["created_at"]:
            warnings.append(f"{path.name}: created_at missing")

        status_counts[job["status"] or "unknown"] += 1
        total_jobs += 1

        steps = map_to_ai_job_steps(data)
        for step in steps:
            name = step["step_name"]
            if name not in VALID_STEP_NAMES:
                warnings.append(f"{path.name}: unknown step '{name}'")
            step_counts[name] += 1
            total_steps += 1

    print("=" * 50)
    print("AI/tasks -> PostgreSQL sdlc schema dry-run")
    print("=" * 50)
    print(f"\nFiles: {len(records)}")
    print(f"Total jobs: {total_jobs}")
    print(f"Total steps: {total_steps}")
    print(f"Broken: {broken}")

    print("\n[Jobs by status]")
    for status, count in sorted(status_counts.items()):
        print(f"  {status}: {count}")

    print("\n[Steps by name]")
    for name, count in sorted(step_counts.items()):
        print(f"  {name}: {count}")

    if warnings:
        print(f"\n[Warnings: {len(warnings)}]")
        for w in warnings:
            print(f"  ! {w}")
    else:
        print("\n[OK] No warnings")

    print("\n[Ready to migrate]")
    print(f"  ai_jobs:      {total_jobs}")
    print(f"  ai_job_steps: {total_steps}")
    print("\nRun with --apply to insert into sdlc schema.")


def run_apply(records: list[tuple[Path, dict]]) -> None:
    conn = get_conn()
    if conn is None:
        sys.exit(1)

    inserted_jobs = 0
    updated_jobs = 0
    inserted_steps = 0
    errors: list[str] = []

    try:
        with conn.cursor() as cur:
            for path, data in records:
                job = map_to_ai_job(data)
                steps = map_to_ai_job_steps(data)

                if not job["branch"]:
                    errors.append(f"{path.name}: branch 없음, 건너뜀")
                    continue

                try:
                    cur.execute("""
                        INSERT INTO sdlc.ai_jobs
                            (sprint, task_name, branch, status, pr_url, test_generated,
                             review_completed, adr_required, duration_sec, consume_tokens,
                             cache_tokens, retry_count, last_error, source_json, impact,
                             created_at, completed_at)
                        VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                                %s::jsonb, %s::jsonb, %s, %s)
                        ON CONFLICT (branch) DO UPDATE SET
                            status           = EXCLUDED.status,
                            pr_url           = EXCLUDED.pr_url,
                            test_generated   = EXCLUDED.test_generated,
                            review_completed = EXCLUDED.review_completed,
                            adr_required     = EXCLUDED.adr_required,
                            retry_count      = EXCLUDED.retry_count,
                            last_error       = EXCLUDED.last_error,
                            completed_at     = EXCLUDED.completed_at,
                            source_json      = EXCLUDED.source_json,
                            impact           = EXCLUDED.impact,
                            updated_at       = CURRENT_TIMESTAMP
                        RETURNING id, (xmax = 0) AS inserted
                    """, (
                        job["sprint"], job["task_name"], job["branch"], job["status"],
                        job["pr_url"], job["test_generated"], job["review_completed"],
                        job["adr_required"], job["duration_sec"], job["consume_tokens"],
                        job["cache_tokens"], job["retry_count"], job["last_error"],
                        job["source_json"], job["impact"],
                        job["created_at"], job["completed_at"],
                    ))
                    row = cur.fetchone()
                    job_id, is_inserted = row
                    if is_inserted:
                        inserted_jobs += 1
                    else:
                        updated_jobs += 1

                    # ai_job_steps: DELETE + re-INSERT
                    cur.execute("DELETE FROM sdlc.ai_job_steps WHERE job_id = %s", (job_id,))
                    for step in steps:
                        cur.execute("""
                            INSERT INTO sdlc.ai_job_steps
                                (job_id, step_name, status, summary, started_at, completed_at)
                            VALUES (%s, %s, %s, %s, %s, %s)
                        """, (
                            job_id, step["step_name"], step["status"], step["summary"],
                            step["started_at"], step["completed_at"],
                        ))
                        inserted_steps += 1

                except Exception as e:
                    errors.append(f"{path.name}: {e}")
                    conn.rollback()
                    continue

        conn.commit()
    finally:
        conn.close()

    print("=" * 50)
    print("AI/tasks -> PostgreSQL sdlc schema apply")
    print("=" * 50)
    print(f"\nai_jobs  inserted: {inserted_jobs}")
    print(f"ai_jobs  updated:  {updated_jobs}")
    print(f"ai_job_steps inserted: {inserted_steps}")

    if errors:
        print(f"\n[Errors: {len(errors)}]")
        for e in errors:
            print(f"  ! {e}")
        sys.exit(1)
    else:
        print("\n[OK] 마이그레이션 완료")


def main() -> None:
    args = parse_args()
    records = load_task_files()
    if not records:
        print(f"AI/tasks/ 에서 sprint*.json 파일을 찾을 수 없습니다: {TASKS_DIR}")
        sys.exit(1)

    if args.apply:
        run_apply(records)
    else:
        run_dry_run(records)


if __name__ == "__main__":
    main()
