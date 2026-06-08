#!/usr/bin/env python3
"""
완료된 task JSON을 순회하여 sdlc.ai_model_runs에 backfill한다.

대상: status=done이고 consume_tokens != null인 task JSON
이미 ai_model_runs에 행이 있는 job은 건너뜀 (ON CONFLICT DO NOTHING).

사용법:
  python backfill_model_runs.py          -- dry-run (대상 목록만 출력)
  python backfill_model_runs.py --apply  -- 실제 INSERT 실행

DB 연결 실패 시 graceful skip (exit 0).
"""

import argparse
import json
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
TASKS_DIR = REPO_ROOT / "AI" / "tasks"
INSERT_SCRIPT = Path(__file__).parent / "insert_model_run.py"


def load_done_tasks() -> list[dict]:
    tasks = []
    for f in sorted(TASKS_DIR.glob("sprint*.json")):
        try:
            data = json.loads(f.read_text(encoding="utf-8"))
            if data.get("status") == "done" and data.get("consume_tokens") is not None:
                tasks.append(data)
        except Exception:
            pass
    return tasks


def already_in_db(branch: str) -> bool:
    try:
        import psycopg2
        conn = psycopg2.connect(
            host="localhost", port=5432,
            dbname="platforma_sdlc", user="platforma", password="platforma_dev_password"
        )
        cur = conn.cursor()
        cur.execute(
            "SELECT COUNT(*) FROM sdlc.ai_model_runs r JOIN sdlc.ai_jobs j ON j.id = r.job_id WHERE j.branch = %s",
            (branch,)
        )
        count = cur.fetchone()[0]
        conn.close()
        return count > 0
    except Exception:
        return False


def run_insert(branch: str, created_at: str) -> bool:
    for python in ("python", "python3"):
        try:
            result = subprocess.run(
                [python, str(INSERT_SCRIPT), "--branch", branch, "--created-at", created_at],
                capture_output=True, text=True, timeout=60
            )
            if result.returncode == 0:
                print(result.stdout.strip())
                return True
            else:
                print(f"  FAIL: {result.stderr.strip()[:100]}", file=sys.stderr)
                return False
        except FileNotFoundError:
            continue
    return False


def main() -> None:
    parser = argparse.ArgumentParser(description="ai_model_runs backfill")
    parser.add_argument("--apply", action="store_true", help="실제 INSERT 실행 (없으면 dry-run)")
    args = parser.parse_args()

    tasks = load_done_tasks()
    print(f"backfill 대상 (status=done, consume_tokens!=null): {len(tasks)}개")
    print()

    skipped = 0
    inserted = 0
    failed = 0

    for task in tasks:
        branch = task["branch"]
        created_at = task.get("created_at", "")

        if already_in_db(branch):
            print(f"  SKIP (already exists): {branch}")
            skipped += 1
            continue

        print(f"  INSERT: {branch} created_at={created_at}")
        if args.apply:
            ok = run_insert(branch, created_at)
            if ok:
                inserted += 1
            else:
                failed += 1

    print()
    if args.apply:
        print(f"완료: inserted={inserted} skipped={skipped} failed={failed}")
    else:
        print(f"dry-run 완료: {len(tasks)}개 대상 (--apply로 실제 실행)")


if __name__ == "__main__":
    main()
