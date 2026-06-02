"""
PR 머지 감지 자동 동기화 스크립트.

GitHub Actions 'PR Merge — SDLC Task Sync' 워크플로우에서 호출된다.
브랜치명으로 AI/tasks/*.json을 탐색하여 task JSON, SPRINT.md, cost-log.md를 갱신한다.
"""

import json
import os
import re
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

BRANCH = os.environ.get("BRANCH", "")
PR_URL = os.environ.get("PR_URL", "")
PR_NUMBER = os.environ.get("PR_NUMBER", "")
CHANGED_FILES = int(os.environ.get("CHANGED_FILES", "0"))
PR_TITLE = os.environ.get("PR_TITLE", "")
GITHUB_STEP_SUMMARY = os.environ.get("GITHUB_STEP_SUMMARY", "")
GH_TOKEN = os.environ.get("GH_TOKEN", "")


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

    now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    data["status"] = "done"
    data["completed_at"] = now
    data["pr_url"] = PR_URL

    # steps[]에 merge_sync 기록 (동일 PR 번호 중복 방지)
    steps = data.get("steps", [])
    already_synced = any(
        s.get("name") == "merge_sync" and s.get("summary", "").find(f"PR #{PR_NUMBER}") != -1
        for s in steps
    )
    if not already_synced:
        steps.append({
            "name": "merge_sync",
            "status": "done",
            "started_at": now,
            "completed_at": now,
            "summary": f"PR #{PR_NUMBER} merged and SDLC state synchronized",
        })
    data["steps"] = steps

    task_file.write_text(
        json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )
    print(f"[ok] task JSON updated: {task_file}")
    return True


def update_sprint_md(sprint_num: int) -> bool:
    """SPRINT.md에서 해당 스프린트 섹션의 '- [ ]'를 '- [x]'로 교체 (idempotent)."""
    sprint_path = Path("AI/SPRINT.md")
    if not sprint_path.exists():
        print("[skip] AI/SPRINT.md not found")
        return False

    content = sprint_path.read_text(encoding="utf-8")
    pattern = rf"(## 스프린트 #{sprint_num}[^\n]*\n.*?)(?=\n## 스프린트 #|\Z)"
    match = re.search(pattern, content, re.DOTALL)

    if not match:
        print(f"[skip] sprint #{sprint_num} section not found in SPRINT.md")
        return False

    section = match.group(0)
    updated_section = section.replace("- [ ]", "- [x]")

    if section == updated_section:
        print(f"[skip] SPRINT.md sprint #{sprint_num} already all checked")
        return False

    content = content[: match.start()] + updated_section + content[match.end() :]
    sprint_path.write_text(content, encoding="utf-8")
    print(f"[ok] SPRINT.md sprint #{sprint_num} items checked")
    return True


def append_cost_log(sprint_num: int, task_name: str, task_data: dict) -> bool:
    """cost-log.md 마지막 행에 추가. 동일 task_name 행이 이미 있으면 skip."""
    cost_log = Path("AI/cost-log.md")
    if not cost_log.exists():
        print("[skip] AI/cost-log.md not found")
        return False

    content = cost_log.read_text(encoding="utf-8")

    # 중복 방지: 동일 task_name 행이 이미 있으면 skip
    if f"| {task_name} |" in content:
        print(f"[skip] cost-log.md already has entry for task '{task_name}'")
        return False

    if CHANGED_FILES <= 2:
        size = "S"
    elif CHANGED_FILES <= 10:
        size = "M"
    elif CHANGED_FILES <= 30:
        size = "L"
    else:
        size = "XL"

    # 위험도 추출 (task JSON impact.risk)
    impact = task_data.get("impact")
    risk = impact.get("risk", "") if isinstance(impact, dict) else ""

    # duration_sec: completed_at - created_at
    duration_sec = "—"
    created_at = task_data.get("created_at")
    completed_at_val = task_data.get("completed_at")
    if created_at and completed_at_val:
        try:
            fmt = "%Y-%m-%dT%H:%M:%SZ"
            t0 = datetime.strptime(created_at, fmt).replace(tzinfo=timezone.utc)
            t1 = datetime.strptime(completed_at_val, fmt).replace(tzinfo=timezone.utc)
            duration_sec = str(int((t1 - t0).total_seconds()))
        except Exception:
            pass

    # consume_tokens / cache_tokens: task JSON에서 읽기 (없으면 —)
    consume_tokens = task_data.get("consume_tokens") or "—"
    cache_tokens = task_data.get("cache_tokens") or "—"

    today = datetime.now().strftime("%Y-%m-%d")
    note = PR_TITLE if PR_TITLE else f"PR #{PR_NUMBER}"
    if risk:
        note = f"{note} [risk:{risk}]"

    new_row = f"| {today} | #{sprint_num} | {task_name} | claude-sonnet-4-6 | {size} | {duration_sec} | {consume_tokens} | {cache_tokens} | {note} |"

    cost_log.write_text(content.rstrip() + "\n" + new_row + "\n", encoding="utf-8")
    print(f"[ok] cost-log.md row added: {new_row}")
    return True


def archive_plan_file(task_name: str) -> bool:
    """task_name에 해당하는 .claude/plan/ 명세 파일을 processed/로 이동. idempotent."""
    plan_dir = Path(".claude/plan")
    processed_dir = plan_dir / "processed"
    if not plan_dir.exists():
        print("[skip] .claude/plan/ 디렉토리 없음")
        return False

    # YYYY-MM-DD_NNN_PlanName.md 또는 YYYY-MM-DD_PlanName.md 패턴 모두 탐색
    matched: list[Path] = [
        f for f in plan_dir.glob("*.md")
        if f.name != "README.md" and f.stem.endswith(f"_{task_name}")
    ]

    if not matched:
        print(f"[skip] .claude/plan/ 에서 '{task_name}' 명세 파일 없음")
        return False

    processed_dir.mkdir(exist_ok=True)
    moved = []
    for src in matched:
        dst = processed_dir / src.name
        if dst.exists():
            print(f"[skip] 이미 이동됨: {src.name}")
            continue
        src.rename(dst)
        moved.append(src.name)
        print(f"[ok] plan 파일 archived: {src.name} → processed/")

    return bool(moved)


def post_pr_comment(body: str) -> None:
    """PR에 댓글을 추가한다. GH_TOKEN이 없으면 skip."""
    if not PR_NUMBER or not GH_TOKEN:
        print("[skip] PR comment 스킵 (GH_TOKEN 또는 PR_NUMBER 없음)")
        return
    try:
        env = {**os.environ, "GH_TOKEN": GH_TOKEN}
        subprocess.run(
            ["gh", "pr", "comment", PR_NUMBER, "--body", body],
            check=True, capture_output=True, env=env
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

    print(f"Processing PR #{PR_NUMBER} from branch: {BRANCH}")

    task_file = find_task_file(BRANCH)
    if task_file is None:
        msg = f"No task JSON found for branch '{BRANCH}'. Skipped SDLC sync."
        print(f"[skip] {msg}")
        comment_body = (
            f"⚠️ **AI_SDLC**: PR #{PR_NUMBER} 머지 — `{BRANCH}` 브랜치에 task JSON 없음.\n\n"
            f"SDLC 파이프라인 없이 머지되었습니다. task JSON이 생성되지 않은 작업입니다."
        )
        post_pr_comment(comment_body)
        write_summary([
            "## PR Merge SDLC Sync",
            f"- Branch: `{BRANCH}`  PR: `#{PR_NUMBER}`",
            f"- ⚠️ Warning: {msg}",
        ])
        sys.exit(0)

    data = json.loads(task_file.read_text(encoding="utf-8"))
    sprint_num = data.get("sprint")
    task_name = data.get("task", BRANCH.split("_", 1)[-1] if "_" in BRANCH else BRANCH)
    status_before = data.get("status", "unknown")

    # task JSON 갱신 (갱신 여부 = /pr 스킬 미실행 경로 여부)
    was_pending = update_task_json(task_file)

    # SPRINT.md 갱신 (항상 실행)
    sprint_updated = False
    if sprint_num is not None:
        sprint_updated = update_sprint_md(sprint_num)

    # cost-log 추가 (/pr 스킬 미실행 경로에서만)
    cost_updated = False
    if was_pending and sprint_num is not None:
        cost_updated = append_cost_log(sprint_num, task_name, data)

    # plan 명세 파일 archived (항상 실행 — /pr 스킬 실행 여부 무관)
    plan_archived = archive_plan_file(task_name)

    print("Done.")

    # GitHub Actions Job Summary
    status_after = "done" if was_pending else status_before
    write_summary([
        "## PR Merge SDLC Sync",
        "",
        f"| 항목 | 값 |",
        f"|------|-----|",
        f"| Branch | `{BRANCH}` |",
        f"| PR | [#{PR_NUMBER}]({PR_URL}) |",
        f"| Task file | `{task_file}` |",
        f"| Status | `{status_before}` → `{status_after}` |",
        f"| SPRINT updated | {'✅ yes' if sprint_updated else '➖ no (already checked or not found)'} |",
        f"| cost-log updated | {'✅ yes' if cost_updated else '➖ no (skipped or already exists)'} |",
        f"| plan file archived | {'✅ yes' if plan_archived else '➖ no (not found or already archived)'} |",
        f"| Warnings | None |",
    ])


if __name__ == "__main__":
    main()
