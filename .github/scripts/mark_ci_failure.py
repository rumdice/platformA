"""
CI 빌드/포맷/테스트 실패 시 task JSON의 last_error를 갱신하고 PR에 댓글을 추가한다.

ci.yml의 'Update task on CI failure' 스텝에서 `if: failure()` 조건으로 호출된다.
status는 변경하지 않는다 — 개발자가 수정 후 재push하면 자연히 해소된다.
"""

import json
import os
import subprocess
from datetime import datetime, timezone
from pathlib import Path

BRANCH = os.environ.get("BRANCH", "")
PR_NUMBER = os.environ.get("PR_NUMBER", "")
GH_TOKEN = os.environ.get("GH_TOKEN", "")


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
        # 동일한 CI 실패 댓글이 이미 있으면 추가하지 않음
        result = subprocess.run(
            ["gh", "pr", "view", PR_NUMBER, "--json", "comments",
             "--jq", "[.comments[].body] | map(select(startswith(\"❌ **CI 실패**\"))) | length"],
            capture_output=True, text=True, env=env,
        )
        if result.returncode == 0 and result.stdout.strip() not in ("", "0"):
            print(f"[skip] CI 실패 댓글 이미 존재 — 중복 방지")
            return

        subprocess.run(
            ["gh", "pr", "comment", PR_NUMBER, "--body", body],
            check=True, capture_output=True, env=env,
        )
        print(f"[ok] PR #{PR_NUMBER} CI 실패 댓글 추가")
    except Exception as e:
        print(f"[warn] PR comment 실패: {e}")


def main() -> None:
    if not BRANCH:
        print("[skip] BRANCH not set — PR 외 push로 간주")
        return

    task_file, data = find_task_file(BRANCH)
    if data is None:
        print(f"[skip] No task JSON for branch '{BRANCH}'")
        return

    if data.get("status") == "done":
        print("[skip] task already done — CI 재실행으로 간주")
        return

    now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    data["last_error"] = f"CI 실패 ({now}) — 빌드·포맷·테스트 중 하나 이상 실패. PR Actions 탭 확인."
    task_file.write_text(
        json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )
    print(f"[ok] task JSON last_error 갱신: {task_file}")

    post_pr_comment(
        f"❌ **CI 실패** — PR #{PR_NUMBER}\n\n"
        f"빌드·포맷·테스트 중 하나가 실패했습니다. "
        f"**Actions** 탭에서 실패 원인을 확인하고 수정 후 재push 하세요.\n\n"
        f"**일반적인 해결 절차:**\n"
        f"1. `/qa-failure` 스킬로 실패 원인 자동 분석\n"
        f"2. 수정 후 커밋·push\n"
        f"3. CI 자동 재실행 확인\n\n"
        f"> task JSON `last_error` 필드가 갱신되었습니다."
    )


if __name__ == "__main__":
    main()
