#!/usr/bin/env python3
"""
AI/tasks/*.json → PlatformA.SdlcDB.Lib (PostgreSQL sdlc 스키마) 마이그레이션 스크립트.

현재는 --dry-run 전용. DB 연결 없이 JSON 파싱 및 매핑 가능 여부를 검증한다.
--apply 구현은 Sprint #42 이후.

사용법:
  python .github/scripts/migrate_tasks_to_postgres.py --dry-run
  python .github/scripts/migrate_tasks_to_postgres.py          # 기본도 dry-run
"""

import argparse
import json
import sys
from pathlib import Path
from collections import defaultdict


REPO_ROOT = Path(__file__).resolve().parent.parent.parent
TASKS_DIR = REPO_ROOT / "AI" / "tasks"

VALID_STEP_NAMES = {
    "plan", "requirement", "impact", "start", "test_gen",
    "done", "review", "pr", "gate_check", "merge_sync",
    "ci_failure", "qa_failure", "doc_sync", "pr_closed_unmerged",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="AI/tasks JSON → PostgreSQL 마이그레이션")
    parser.add_argument("--dry-run", action="store_true", default=True,
                        help="DB 연결 없이 매핑 가능 여부만 검증 (기본값)")
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
        "source_json": json.dumps(data),        # jsonb
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
    print(f"  ai_failures:  0  (no separate records)")
    print(f"  ai_model_runs: 0 (no separate records)")
    print("\nRun with --apply to insert into sdlc schema (Sprint #42).")


def main() -> None:
    args = parse_args()
    records = load_task_files()
    if not records:
        print(f"AI/tasks/ 에서 sprint*.json 파일을 찾을 수 없습니다: {TASKS_DIR}")
        sys.exit(1)
    run_dry_run(records)


if __name__ == "__main__":
    main()
