"""
Sprint #N-#M의 누락된 토큰 정보를 역산하여 AI/cost-log.md를 업데이트한다.

사용법:
  python backfill_cost_log.py --sprint 39
  python backfill_cost_log.py --sprint-range 39 44
  python backfill_cost_log.py --sprint-range 39 44 --dry-run
"""

import argparse
import glob
import json
import os
import re
import subprocess
import sys
from pathlib import Path


def find_repo_root() -> Path:
    """git 루트를 찾는다."""
    try:
        result = subprocess.run(
            ["git", "rev-parse", "--show-toplevel"],
            capture_output=True, text=True, check=True
        )
        return Path(result.stdout.strip())
    except Exception:
        return Path.cwd()


def find_task_json(repo_root: Path, sprint_num: int) -> Path | None:
    """AI/tasks/sprint{N}_*.json 파일을 탐색한다."""
    pattern = str(repo_root / "AI" / "tasks" / f"sprint{sprint_num}_*.json")
    matches = glob.glob(pattern)
    return Path(matches[0]) if matches else None


def get_created_at(task_file: Path) -> str | None:
    """task JSON에서 created_at을 추출한다."""
    try:
        with open(task_file, encoding="utf-8") as f:
            data = json.load(f)
        return data.get("created_at")
    except Exception as e:
        print(f"  [WARN] task JSON 읽기 실패: {e}", file=sys.stderr)
        return None


def calc_tokens(created_at: str) -> tuple[str, str, str]:
    """count_tokens.py를 호출하여 토큰을 계산한다."""
    script = Path(__file__).parent / "count_tokens.py"
    for python in ("python", "python3"):
        try:
            result = subprocess.run(
                [python, str(script), created_at],
                capture_output=True, text=True, timeout=30
            )
            if result.returncode == 0 and result.stdout.strip():
                lines = result.stdout.strip().splitlines()
                data = {}
                for line in lines:
                    if "=" in line:
                        k, v = line.split("=", 1)
                        data[k.strip()] = v.strip()
                return (
                    data.get("duration_sec", "—"),
                    data.get("consume_tokens", "—"),
                    data.get("cache_tokens", "—"),
                )
        except Exception:
            continue
    return ("—", "—", "—")


def update_cost_log(cost_log: Path, sprint_num: int,
                    duration: str, consume: str, cache: str,
                    dry_run: bool) -> bool:
    """
    cost-log.md에서 스프린트 #N 행을 찾아 — 값을 실제 값으로 교체한다.
    반환: 성공 여부
    """
    content = cost_log.read_text(encoding="utf-8")

    # 패턴: | ... | #N | ... | — | — | — | ...
    # 스프린트 번호가 포함된 행에서 duration/consume/cache 컬럼의 — 교체
    pattern = rf"(\|\s*\d{{4}}-\d{{2}}-\d{{2}}\s*\|\s*#{sprint_num}\s*\|[^|]*\|[^|]*\|[^|]*\|)\s*—\s*(\|)\s*—\s*(\|)\s*—\s*(\|)"

    def replacer(m):
        return f"{m.group(1)} {duration} {m.group(2)} {consume} {m.group(3)} {cache} {m.group(4)}"

    new_content, count = re.subn(pattern, replacer, content)

    if count == 0:
        # 이미 값이 있거나 행이 없는 경우
        print(f"  [SKIP] Sprint #{sprint_num}: 행을 찾지 못했거나 이미 값이 있습니다.")
        return False

    if dry_run:
        print(f"  [DRY-RUN] Sprint #{sprint_num}: {duration} / {consume} / {cache} (미적용)")
        return True

    cost_log.write_text(new_content, encoding="utf-8")
    print(f"  [OK] Sprint #{sprint_num}: duration={duration}, consume={consume}, cache={cache}")
    return True


def process_sprint(repo_root: Path, sprint_num: int, dry_run: bool) -> None:
    print(f"\nSprint #{sprint_num} 처리 중...")

    task_file = find_task_json(repo_root, sprint_num)
    if task_file is None:
        print(f"  [SKIP] task JSON 없음: AI/tasks/sprint{sprint_num}_*.json")
        return

    created_at = get_created_at(task_file)
    if not created_at:
        print(f"  [SKIP] created_at 없음: {task_file.name}")
        return

    print(f"  created_at: {created_at}")
    duration, consume, cache = calc_tokens(created_at)
    print(f"  계산 결과: duration={duration}, consume={consume}, cache={cache}")

    cost_log = repo_root / "AI" / "cost-log.md"
    if not cost_log.exists():
        print(f"  [ERROR] cost-log.md를 찾을 수 없음: {cost_log}")
        return

    update_cost_log(cost_log, sprint_num, duration, consume, cache, dry_run)


def main() -> None:
    parser = argparse.ArgumentParser(description="cost-log.md 누락 토큰 역산")
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--sprint", type=int, metavar="N", help="단일 스프린트 번호")
    group.add_argument("--sprint-range", type=int, nargs=2, metavar=("START", "END"),
                       help="스프린트 범위 (포함)")
    parser.add_argument("--dry-run", action="store_true", help="변경 없이 결과만 출력")
    args = parser.parse_args()

    repo_root = find_repo_root()
    print(f"저장소 루트: {repo_root}")

    if args.sprint:
        sprints = [args.sprint]
    else:
        start, end = args.sprint_range
        sprints = list(range(start, end + 1))

    for sprint_num in sprints:
        process_sprint(repo_root, sprint_num, args.dry_run)

    print("\n완료.")


if __name__ == "__main__":
    main()
