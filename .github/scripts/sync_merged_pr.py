"""
PR 머지 감지 자동 동기화 스크립트.

GitHub Actions 'PR Merge — SDLC Task Sync' 워크플로우에서 호출된다.
브랜치명으로 AI/tasks/*.json 또는 AI/sprints/*.md를 탐색하여 task JSON을 갱신한다.

Phase C (2026-06-10~): task JSON 신규 생성 중단 — AI/sprints/*.md frontmatter로 판별.
AI/SPRINT.md, AI/cost-log.md는 삭제됨 — DB/sprints/*.md가 단일 진실 공급원.
"""

import json
import os
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Optional

BRANCH = os.environ.get("BRANCH", "")
PR_URL = os.environ.get("PR_URL", "")
PR_NUMBER = os.environ.get("PR_NUMBER", "")
CHANGED_FILES = int(os.environ.get("CHANGED_FILES", "0"))
PR_TITLE = os.environ.get("PR_TITLE", "")
GITHUB_STEP_SUMMARY = os.environ.get("GITHUB_STEP_SUMMARY", "")
GH_TOKEN = os.environ.get("GH_TOKEN", "")


def find_task_file(branch: str) -> Optional[Path]:
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


def find_sprint_file(branch: str) -> Optional[Path]:
    """AI/sprints/*.md frontmatter에서 branch 필드가 일치하는 파일을 찾는다.

    Phase C(2026-06-10~)부터 task JSON 대신 sprint-NNN.md가 단일 진실 공급원.
    GitHub Actions checkout에서 접근 가능한 git 파일만 사용한다 (DB 접근 없음).
    """
    sprints_dir = Path("AI/sprints")
    if not sprints_dir.exists():
        return None
    for md_file in sorted(sprints_dir.glob("sprint-*.md")):
        try:
            content = md_file.read_text(encoding="utf-8")
            if not content.startswith("---"):
                continue
            end = content.find("---", 3)
            if end == -1:
                continue
            frontmatter = content[3:end]
            for line in frontmatter.splitlines():
                if line.strip().startswith("branch:"):
                    value = line.split(":", 1)[1].strip()
                    if value == branch:
                        return md_file
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
        # Phase C: task JSON 없음 → AI/sprints/*.md frontmatter로 판별
        sprint_file = find_sprint_file(BRANCH)
        if sprint_file is not None:
            print(f"[ok] Phase C job 확인됨 ({sprint_file.name}) — task JSON 없음은 정상")
            write_summary([
                "## PR Merge SDLC Sync",
                "",
                f"| 항목 | 값 |",
                f"|------|-----|",
                f"| Branch | `{BRANCH}` |",
                f"| PR | [#{PR_NUMBER}]({PR_URL}) |",
                f"| Sprint file | `{sprint_file}` |",
                f"| Phase | Phase C (task JSON 없음 — 정상) |",
                f"| Warnings | None |",
            ])
            sys.exit(0)

        # sprint 파일도 없음 → 진짜 SDLC 누락
        msg = f"No task JSON or sprint file found for branch '{BRANCH}'. Skipped SDLC sync."
        print(f"[warn] {msg}")
        comment_body = (
            f"⚠️ **AI_SDLC**: PR #{PR_NUMBER} 머지 — `{BRANCH}` 브랜치에 task JSON 및 sprint 파일 없음.\n\n"
            f"SDLC 파이프라인 없이 머지되었습니다. `/plan` 없이 직접 작업된 브랜치입니다."
        )
        post_pr_comment(comment_body)
        write_summary([
            "## PR Merge SDLC Sync",
            f"- Branch: `{BRANCH}`  PR: `#{PR_NUMBER}`",
            f"- ⚠️ Warning: {msg}",
        ])
        sys.exit(0)

    data = json.loads(task_file.read_text(encoding="utf-8"))
    task_name = data.get("task", BRANCH.split("_", 1)[-1] if "_" in BRANCH else BRANCH)
    status_before = data.get("status", "unknown")

    # task JSON 갱신 (갱신 여부 = /pr 스킬 미실행 경로 여부)
    was_pending = update_task_json(task_file)

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
        f"| plan file archived | {'✅ yes' if plan_archived else '➖ no (not found or already archived)'} |",
        f"| Warnings | None |",
    ])


if __name__ == "__main__":
    main()
