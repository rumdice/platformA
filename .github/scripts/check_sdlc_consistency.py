#!/usr/bin/env python3
"""
AI_SDLC 정합성 검사 - AI/tasks/*.json ↔ PostgreSQL sdlc.ai_jobs 비교.

사용법:
  python check_sdlc_consistency.py --check           기본 모드: 불일치 있어도 exit 0
  python check_sdlc_consistency.py --check --strict  strict 모드: critical mismatch 시 exit 1

DB 연결 실패 시 graceful skip (exit 0).
"""

import argparse
import json
import os
import sys
from pathlib import Path

CONN_STR = os.environ.get(
    "SDLC_DB_CONNECTION",
    "Host=localhost;Port=5432;Database=platforma_sdlc;Username=platforma;Password=platforma_dev_password",
)

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
TASKS_DIR = REPO_ROOT / "AI" / "tasks"


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
        print("[consistency] psycopg2 미설치 - DB 검사 건너뜀", file=sys.stderr)
        return None
    except Exception as e:
        print(f"[consistency] PostgreSQL 연결 실패: {e} - DB 검사 건너뜀", file=sys.stderr)
        return None


def load_task_jsons() -> dict:
    tasks = {}
    if not TASKS_DIR.exists():
        return tasks
    for f in TASKS_DIR.glob("sprint*.json"):
        try:
            data = json.loads(f.read_text(encoding="utf-8"))
            branch = data.get("branch")
            if branch:
                tasks[branch] = data
        except Exception:
            pass
    return tasks


def load_db_jobs(conn) -> dict:
    jobs = {}
    try:
        cur = conn.cursor()
        cur.execute(
            "SELECT branch, sprint, task_name, status, test_generated, review_completed, pr_url, impact, updated_at FROM sdlc.ai_jobs"
        )
        for row in cur.fetchall():
            branch, sprint, task, status, test_gen, review, pr_url, impact, updated_at = row
            jobs[branch] = {
                "branch": branch,
                "sprint": sprint,
                "task": task,
                "status": status,
                "test_generated": test_gen,
                "review_completed": review,
                "pr_url": pr_url,
                "impact": impact,
                "updated_at": updated_at,
            }
    except Exception as e:
        print(f"[consistency] ai_jobs 조회 실패: {e}", file=sys.stderr)
    return jobs


def load_db_step_counts(conn) -> dict:
    counts = {}
    try:
        cur = conn.cursor()
        cur.execute(
            """
            SELECT j.branch, COUNT(s.id)
            FROM sdlc.ai_jobs j
            LEFT JOIN sdlc.ai_job_steps s ON s.job_id = j.id
            GROUP BY j.branch
            """
        )
        for branch, cnt in cur.fetchall():
            counts[branch] = cnt
    except Exception as e:
        print(f"[consistency] ai_job_steps 조회 실패: {e}", file=sys.stderr)
    return counts


def load_db_model_run_branches(conn) -> set:
    branches = set()
    try:
        cur = conn.cursor()
        cur.execute(
            """
            SELECT DISTINCT j.branch
            FROM sdlc.ai_model_runs r
            JOIN sdlc.ai_jobs j ON j.id = r.job_id
            """
        )
        for (branch,) in cur.fetchall():
            branches.add(branch)
    except Exception as e:
        print(f"[consistency] ai_model_runs 조회 실패: {e}", file=sys.stderr)
    return branches


def check(strict: bool) -> int:
    print("AI_SDLC Consistency Check")
    print("=" * 40)

    tasks = load_task_jsons()
    print(f"Task JSON files: {len(tasks)}")

    conn = get_conn()
    if conn is None:
        print("\nDB unavailable; consistency check skipped")
        print("Result: SKIP")
        return 0

    try:
        db_jobs = load_db_jobs(conn)
        db_step_counts = load_db_step_counts(conn)
        db_model_run_branches = load_db_model_run_branches(conn)

        print(f"DB jobs: {len(db_jobs)}")
        print(f"DB step records (per job): measured")
        print(f"DB model runs: {len(db_model_run_branches)} branches with runs")
        print()

        missing_in_db = []
        missing_in_files = []
        files_archived = []  # Phase C: DB에만 있고 JSON 없음 + status=done → 정상
        status_mismatches = []
        gate_mismatches = []
        step_count_mismatches = []
        model_run_missing = []
        model_run_legacy = []  # consume_tokens=None → cost 추적 이전 레거시
        stuck_sprints = []    # coding 상태로 24시간 이상 미갱신

        for branch, task in tasks.items():
            if branch not in db_jobs:
                missing_in_db.append(branch)
                continue

            db = db_jobs[branch]

            # status 비교
            if task.get("status") != db.get("status"):
                status_mismatches.append(
                    f"{branch}: JSON={task.get('status')} DB={db.get('status')}"
                )

            # gate 비교
            if bool(task.get("test_generated")) != bool(db.get("test_generated")):
                gate_mismatches.append(
                    f"{branch}: test_generated JSON={task.get('test_generated')} DB={db.get('test_generated')}"
                )
            if bool(task.get("review_completed")) != bool(db.get("review_completed")):
                gate_mismatches.append(
                    f"{branch}: review_completed JSON={task.get('review_completed')} DB={db.get('review_completed')}"
                )

            # step 수 비교
            json_steps = len(task.get("steps", []))
            db_steps = db_step_counts.get(branch, 0)
            if json_steps != db_steps:
                step_count_mismatches.append(
                    f"{branch}: JSON steps={json_steps} DB steps={db_steps}"
                )

            # model run 누락 (완료된 작업만 검사)
            if task.get("status") == "done" and branch not in db_model_run_branches:
                # consume_tokens=None → cost 추적 도입 이전 레거시, LEGACY exception 처리
                if task.get("consume_tokens") is None:
                    model_run_legacy.append(branch)
                else:
                    model_run_missing.append(branch)

        from datetime import datetime as _dt_cls, timezone as _tz
        _now = _dt_cls.now(_tz.utc)
        _stuck_threshold_hours = 24

        for branch in db_jobs:
            db = db_jobs[branch]
            if branch not in tasks:
                # Phase C: status=done이면 JSON 없음이 정상 (archived)
                if db.get("status") == "done":
                    files_archived.append(branch)
                else:
                    missing_in_files.append(branch)

            # stuck sprint 감지: coding/analyzing 상태로 24시간 이상 미갱신
            if db.get("status") in ("coding", "analyzing", "failed", "testing"):
                updated_at = db.get("updated_at")
                if updated_at:
                    if updated_at.tzinfo is None:
                        updated_at = updated_at.replace(tzinfo=_tz.utc)
                    hours_stale = (_now - updated_at).total_seconds() / 3600
                    if hours_stale > _stuck_threshold_hours:
                        stuck_sprints.append(
                            f"{branch}: status={db.get('status')} stale={hours_stale:.0f}h"
                        )

        print(f"Missing in DB:          {len(missing_in_db)}")
        print(f"Missing in files:       {len(missing_in_files)} (in-progress, no JSON)")
        print(f"Files archived (Phase C): {len(files_archived)} (done in DB only - normal)")
        print(f"Status mismatches:      {len(status_mismatches)}")
        print(f"Gate mismatches:        {len(gate_mismatches)}")
        print(f"Step count mismatches:  {len(step_count_mismatches)}")
        print(f"Model run missing:      {len(model_run_missing)}")
        print(f"Model run legacy:       {len(model_run_legacy)} (no cost tracking - LEGACY exception)")
        print(f"Stuck sprints (>24h):   {len(stuck_sprints)}")

        has_warning = any([
            missing_in_db, missing_in_files, status_mismatches,
            gate_mismatches, step_count_mismatches, model_run_missing,
            stuck_sprints,
        ])

        if has_warning or model_run_legacy or files_archived:
            print()
            if missing_in_db:
                print("WARN - Missing in DB:")
                for b in missing_in_db[:5]:
                    print(f"  {b}")
            if missing_in_files:
                print("WARN - Missing in files (in-progress, no JSON):")
                for b in missing_in_files[:5]:
                    print(f"  {b}")
            if files_archived:
                print(f"INFO - Files archived (Phase C - {len(files_archived)} done-in-DB-only, not a failure):")
                for b in files_archived[:5]:
                    print(f"  {b}")
                if len(files_archived) > 5:
                    print(f"  ... and {len(files_archived) - 5} more")
            if status_mismatches:
                print("WARN - Status mismatches:")
                for m in status_mismatches[:5]:
                    print(f"  {m}")
            if gate_mismatches:
                print("WARN - Gate mismatches:")
                for m in gate_mismatches[:5]:
                    print(f"  {m}")
            if model_run_legacy:
                print(f"LEGACY - Model run missing (no cost tracking, {len(model_run_legacy)} items - not a failure):")
                for b in model_run_legacy[:5]:
                    print(f"  {b}")
                if len(model_run_legacy) > 5:
                    print(f"  ... and {len(model_run_legacy) - 5} more")
            if stuck_sprints:
                print(f"WARN - Stuck sprints (no update for >24h):")
                for s in stuck_sprints[:5]:
                    print(f"  {s}")

        critical = missing_in_db + gate_mismatches
        if not has_warning and not model_run_legacy:
            print("\nResult: OK")
            return 0
        elif strict and critical:
            print("\nResult: FAIL (strict mode)")
            return 1
        else:
            print("\nResult: WARN")
            return 0
    finally:
        conn.close()


def main() -> None:
    parser = argparse.ArgumentParser(description="AI_SDLC DB/JSON 정합성 검사")
    parser.add_argument("--check", action="store_true", required=True)
    parser.add_argument("--strict", action="store_true", help="critical mismatch 시 exit 1")
    args = parser.parse_args()
    sys.exit(check(args.strict))


if __name__ == "__main__":
    main()
