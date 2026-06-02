"""
JSONL 파싱으로 task 기간 토큰 소비량 자동 계산.

사용법: python3 count_tokens.py <created_at_iso8601>
출력:
  consume_tokens=NNN   (input + output + cache_read + cache_creation 합산)
  cache_tokens=NNN     (cache_creation_input_tokens 단독 — 별도 비용 분석용)

created_at 이후에 발생한 모든 assistant 메시지의 usage를 합산한다.
메인 세션 파일과 subagents/ 하위 파일을 모두 포함한다.
"""

import json
import os
import pathlib
import re
import sys
from datetime import datetime, timezone
from typing import Optional, Tuple


def get_project_dir() -> Optional[pathlib.Path]:
    """현재 CWD 기반으로 ~/.claude/projects/<hash>/ 경로를 반환한다."""
    home = pathlib.Path.home()
    cwd = os.getcwd()

    # Python on Windows returns Windows-style path (c:\Users\...),
    # but when run from Git Bash may return /c/Users/...
    if re.match(r"^/[a-zA-Z]/", cwd):
        drive = cwd[1].upper()
        rest = cwd[2:].replace("/", "\\")
        win_cwd = drive + ":" + rest
    else:
        win_cwd = cwd

    project_hash = win_cwd.replace(":", "-").replace("\\", "-").replace("/", "-")
    project_dir = home / ".claude" / "projects" / project_hash
    if project_dir.exists():
        return project_dir

    # Fallback: strip leading hyphens if any
    project_hash2 = project_hash.lstrip("-")
    project_dir2 = home / ".claude" / "projects" / project_hash2
    if project_dir2.exists():
        return project_dir2

    return None


def parse_timestamp(ts_str: str) -> Optional[datetime]:
    try:
        return datetime.fromisoformat(ts_str.replace("Z", "+00:00"))
    except Exception:
        return None


def count_tokens(created_at_str: str) -> Tuple[int, int]:
    """
    created_at 이후 모든 assistant 메시지 usage 합산.
    반환: (consume_tokens, cache_tokens)
    consume_tokens = input + output + cache_read + cache_creation
    cache_tokens   = cache_creation_input_tokens
    """
    try:
        fmt = "%Y-%m-%dT%H:%M:%SZ"
        cutoff = datetime.strptime(created_at_str, fmt).replace(tzinfo=timezone.utc)
    except ValueError:
        return (0, 0)

    project_dir = get_project_dir()
    if project_dir is None:
        return (0, 0)

    total_input = total_output = total_cache_read = total_cache_creation = 0
    latest_ts_before_cutoff: Optional[datetime] = None

    for jsonl_file in sorted(project_dir.rglob("*.jsonl")):
        try:
            content = jsonl_file.read_text(encoding="utf-8", errors="ignore")
        except Exception:
            continue
        for line in content.splitlines():
            try:
                entry = json.loads(line)
                if entry.get("type") != "assistant":
                    continue
                ts = parse_timestamp(entry.get("timestamp", ""))
                if ts is None or ts < cutoff:
                    if ts is not None and (latest_ts_before_cutoff is None or ts > latest_ts_before_cutoff):
                        latest_ts_before_cutoff = ts
                    continue
                usage = entry.get("message", {}).get("usage", {})
                total_input += usage.get("input_tokens", 0)
                total_output += usage.get("output_tokens", 0)
                total_cache_read += usage.get("cache_read_input_tokens", 0)
                total_cache_creation += usage.get("cache_creation_input_tokens", 0)
            except Exception:
                continue

    consume_tokens = total_input + total_output + total_cache_read + total_cache_creation
    cache_tokens = total_cache_creation

    # cutoff 이후 데이터가 0인데 cutoff 이전 데이터가 존재하면 created_at 오류 가능성 경고
    if consume_tokens == 0 and latest_ts_before_cutoff is not None:
        import sys as _sys
        print(
            f"WARNING: consume_tokens=0 but JSONL data exists up to "
            f"{latest_ts_before_cutoff.strftime('%Y-%m-%dT%H:%M:%SZ')} "
            f"(cutoff={cutoff.strftime('%Y-%m-%dT%H:%M:%SZ')}). "
            "Check task JSON created_at — it may be set past the actual session end.",
            file=_sys.stderr,
        )

    return (consume_tokens, cache_tokens)


def main() -> None:
    if len(sys.argv) < 2:
        print("consume_tokens=null")
        print("cache_tokens=null")
        sys.exit(1)

    created_at = sys.argv[1]
    consume, cache = count_tokens(created_at)
    print(f"consume_tokens={consume}")
    print(f"cache_tokens={cache}")


if __name__ == "__main__":
    main()
