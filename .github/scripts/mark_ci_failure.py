"""
CI 빌드/포맷/테스트 실패 시 task JSON의 last_error를 갱신하고 PR에 댓글을 추가한다.

ci.yml의 'Update task on CI failure' 스텝에서 `if: failure()` 조건으로 호출된다.
status는 변경하지 않는다 — 개발자가 수정 후 재push하면 자연히 해소된다.

댓글에는 n8n이 파싱할 수 있는 AI_SDLC_FAILURE_RECORD 마커가 포함된다.
n8n이 이 마커를 감지하여 PostgreSQL sdlc.ai_failures에 INSERT한다.
"""

import json
import os
import subprocess
from datetime import datetime, timezone
from pathlib import Path

BRANCH = os.environ.get("BRANCH", "")
PR_NUMBER = os.environ.get("PR_NUMBER", "")
GH_TOKEN = os.environ.get("GH_TOKEN", "")
REPO = os.environ.get("GITHUB_REPOSITORY", "rumdice/platformA")

# 실패 타입 분류 기준
FAILURE_PATTERNS: dict[str, list[str]] = {
    "format_failed": ["CHARSET", "whitespace --verify", "dotnet-format", "Fix file encoding"],
    "style_failed":  ["style --verify-no-changes", "IDE0160", "IDE0161", "IDE0"],
    "test_failed":   ["Failed!  -", "Test Run Failed", "FAILED"],
    "gate_failed":   ["AI_SDLC Gate Check", "SDLC gate"],
}
FIXABLE: dict[str, bool] = {
    "format_failed": True,
    "style_failed":  True,
    "test_failed":   False,
    "gate_failed":   False,
    "build_failed":  False,
}


def classify_failure() -> tuple[str, bool]:
    """환경변수 FAILED_STEP에서 실패 타입을 분류한다."""
    failed_step = os.environ.get("FAILED_STEP", "")
    log_excerpt = os.environ.get("CI_LOG_EXCERPT", "")
    combined = failed_step + " " + log_excerpt

    for ftype, patterns in FAILURE_PATTERNS.items():
        if any(p.lower() in combined.lower() for p in patterns):
            return ftype, FIXABLE.get(ftype, False)
    return "build_failed", False


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

    failure_type, fixable = classify_failure()

    marker = json.dumps({
        "sdlc_ci_failure": True,
        "failure_type": failure_type,
        "branch": BRANCH,
        "fixable_by_ai": fixable,
        "recorded_at": now,
        "marker": "AI_SDLC_FAILURE_RECORD",
    }, ensure_ascii=False)

    post_pr_comment(
        f"<!-- AI_SDLC_FAILURE_RECORD\n{marker}\n-->\n"
        f"❌ **CI 실패** — PR #{PR_NUMBER} / 브랜치: `{BRANCH}`\n\n"
        f"| 항목 | 값 |\n|------|----|\n"
        f"| 실패 유형 | `{failure_type}` |\n"
        f"| 자동 수정 가능 | {'✅ 예 (auto-format 워크플로 시도 중)' if fixable else '❌ 수동 수정 필요'} |\n\n"
        f"- **Actions 탭**: 실패 단계 및 로그 확인\n"
        f"- **task JSON `last_error`**: 실패 시각 기록됨\n"
        f"- **n8n**: 로컬 실행 중이면 sdlc.ai_failures에 자동 기록됨"
    )


if __name__ == "__main__":
    main()
