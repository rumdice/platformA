"""
PR 머지 감지 자동 동기화 스크립트.

GitHub Actions 'PR Merge — SDLC Task Sync' 워크플로우에서 호출된다.
브랜치명으로 AI/tasks/*.json을 탐색하여 task JSON, SPRINT.md, cost-log.md를 갱신한다.
"""

import json
import os
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

BRANCH = os.environ.get("BRANCH", "")
PR_URL = os.environ.get("PR_URL", "")
PR_NUMBER = os.environ.get("PR_NUMBER", "")
CHANGED_FILES = int(os.environ.get("CHANGED_FILES", "0"))
PR_TITLE = os.environ.get("PR_TITLE", "")


def find_task_file(branch: str) -> Path | None:
    tasks_dir = Path("AI/tasks")
    if not tasks_dir.exists():
        return None
    for json_file in sorted(tasks_dir.glob("sprint*.json")):
        try:
            data = json.loads(json_file.read_text(encoding="utf-8"))
            if data.get("branch") == branch:
                return json_file
        except Exception:
            continue
    return None


def update_task_json(task_file: Path) -> bool:
    """task JSON 갱신. status가 이미 'done'이면 skip. 갱신 여부 반환."""
    data = json.loads(task_file.read_text(encoding="utf-8"))
    if data.get("status") == "done":
        print(f"[skip] task JSON already done: {task_file}")
        return False

    data["status"] = "done"
    data["completed_at"] = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    data["pr_url"] = PR_URL

    task_file.write_text(
        json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )
    print(f"[ok] task JSON updated: {task_file}")
    return True


def update_sprint_md(sprint_num: int) -> None:
    """SPRINT.md에서 해당 스프린트 섹션의 '- [ ]'를 '- [x]'로 교체 (idempotent)."""
    sprint_path = Path("AI/SPRINT.md")
    if not sprint_path.exists():
        print("[skip] AI/SPRINT.md not found")
        return

    content = sprint_path.read_text(encoding="utf-8")
    pattern = rf"(## 스프린트 #{sprint_num}[^\n]*\n.*?)(?=\n## 스프린트 #|\Z)"
    match = re.search(pattern, content, re.DOTALL)

    if not match:
        print(f"[skip] sprint #{sprint_num} section not found in SPRINT.md")
        return

    section = match.group(0)
    updated_section = section.replace("- [ ]", "- [x]")

    if section == updated_section:
        print(f"[skip] SPRINT.md sprint #{sprint_num} already all checked")
        return

    content = content[: match.start()] + updated_section + content[match.end() :]
    sprint_path.write_text(content, encoding="utf-8")
    print(f"[ok] SPRINT.md sprint #{sprint_num} items checked")


def append_cost_log(sprint_num: int, task_name: str) -> None:
    """cost-log.md 마지막 행에 추가."""
    if CHANGED_FILES <= 2:
        size = "S"
    elif CHANGED_FILES <= 10:
        size = "M"
    elif CHANGED_FILES <= 30:
        size = "L"
    else:
        size = "XL"

    today = datetime.now().strftime("%Y-%m-%d")
    note = PR_TITLE if PR_TITLE else f"PR #{PR_NUMBER}"
    new_row = f"| {today} | #{sprint_num} | {task_name} | claude-sonnet-4-6 | {size} | {note} |"

    cost_log = Path("AI/cost-log.md")
    if not cost_log.exists():
        print("[skip] AI/cost-log.md not found")
        return

    content = cost_log.read_text(encoding="utf-8")
    cost_log.write_text(content.rstrip() + "\n" + new_row + "\n", encoding="utf-8")
    print(f"[ok] cost-log.md row added: {new_row}")


def main() -> None:
    if not BRANCH:
        print("[error] BRANCH environment variable not set")
        sys.exit(1)

    print(f"Processing PR #{PR_NUMBER} from branch: {BRANCH}")

    task_file = find_task_file(BRANCH)
    if task_file is None:
        print(f"[skip] no task JSON found for branch '{BRANCH}' — nothing to sync")
        sys.exit(0)

    data = json.loads(task_file.read_text(encoding="utf-8"))
    sprint_num = data.get("sprint")
    task_name = data.get("task", BRANCH.split("_", 1)[-1] if "_" in BRANCH else BRANCH)

    # task JSON 갱신 (갱신 여부 = /pr 스킬 미실행 경로 여부)
    was_pending = update_task_json(task_file)

    # SPRINT.md 갱신 (항상 실행)
    if sprint_num is not None:
        update_sprint_md(sprint_num)

    # cost-log 추가 (/pr 스킬 미실행 경로에서만)
    if was_pending and sprint_num is not None:
        append_cost_log(sprint_num, task_name)

    print("Done.")


if __name__ == "__main__":
    main()
