"""
PR이 merge 없이 closed될 때 task JSON을 'abandoned' 상태로 갱신한다.

GitHub Actions 'PR Merge — SDLC Task Sync' 워크플로우의 sync-task-abandoned 잡에서 호출된다.
"""

import json
import os
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

BRANCH = os.environ.get("BRANCH", "")
PR_NUMBER = os.environ.get("PR_NUMBER", "")
PR_URL = os.environ.get("PR_URL", "")
GH_TOKEN = os.environ.get("GH_TOKEN", "")
GITHUB_STEP_SUMMARY = os.environ.get("GITHUB_STEP_SUMMARY", "")


def find_task_file(branch: str) -> tuple[Path | None, dict | None]:
    tasks_dir = Path("AI/tasks")
    if not tasks_dir.exists():
        return None, None
    for json_file in sorted(tasks_dir.glob("sprint*.json")):
        try:
            data = json.loads(json_file.read_text(encoding="utf-8"))
            if data.get("branch") == branch:
                return json_file, data
        except Exception:
            continue
    return None, None


def post_pr_comment(body: str) -> None:
    if not PR_NUMBER or not GH_TOKEN:
        return
    try:
        env = {**os.environ, "GH_TOKEN": GH_TOKEN}
        subprocess.run(
            ["gh", "pr", "comment", PR_NUMBER, "--body", body],
            check=True, capture_output=True, env=env,
        )
        print(f"[ok] PR #{PR_NUMBER} comment 추가 완료")
    except Exception as e:
        print(f"[warn] PR comment 실패: {e}")


def write_summary(lines: list[str]) -> None:
    if not GITHUB_STEP_SUMMARY:
        return
    try:
        with open(GITHUB_STEP_SUMMARY, "a", encoding="utf-8") as f:
            f.write("\n".join(lines) + "\n")
    except Exception as e:
        print(f"[warn] summary 기록 실패: {e}")


def main() -> None:
    if not BRANCH:
        print("[error] BRANCH environment variable not set")
        sys.exit(1)

    print(f"PR #{PR_NUMBER} closed without merge — branch: {BRANCH}")

    task_file, data = find_task_file(BRANCH)
    if data is None:
        print(f"[skip] No task JSON found for branch '{BRANCH}' — SDLC 외 작업으로 간주")
        return

    current_status = data.get("status", "unknown")
    if current_status in ("done", "abandoned"):
        print(f"[skip] task already in terminal status: {current_status}")
        return

    now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    data["status"] = "abandoned"
    data["last_error"] = f"PR #{PR_NUMBER} closed without merge"

    steps = data.get("steps", [])
    steps.append({
        "name": "pr_abandoned",
        "status": "done",
        "started_at": now,
        "completed_at": now,
        "summary": f"PR #{PR_NUMBER} closed without merge — task abandoned",
    })
    data["steps"] = steps

    task_file.write_text(
        json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )
    print(f"[ok] task JSON status: {current_status} → abandoned ({task_file})")

    post_pr_comment(
        f"⚠️ **AI_SDLC**: PR #{PR_NUMBER} 미머지 종료 — 브랜치: `{BRANCH}`\n\n"
        f"task JSON `status`: `{current_status}` → `abandoned` 기록됨."
    )

    write_summary([
        "## PR Closed Without Merge — SDLC Sync",
        "",
        f"| 항목 | 값 |",
        f"|------|-----|",
        f"| Branch | `{BRANCH}` |",
        f"| PR | [#{PR_NUMBER}]({PR_URL}) |",
        f"| Task file | `{task_file}` |",
        f"| Status | `{current_status}` → `abandoned` |",
    ])


if __name__ == "__main__":
    main()
