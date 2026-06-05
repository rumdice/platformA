#!/usr/bin/env python3
"""
/pr 완료 시 sdlc.ai_model_runs 테이블에 토큰 사용량을 기록한다.

사용법:
  python insert_model_run.py --branch <브랜치명> --created-at <ISO8601>

연결 실패 시 stderr 경고 후 exit 0 (cost-log.md 흐름 차단 없음).
"""

import argparse
import json
import os
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Optional

REPO_ROOT = Path(__file__).resolve().parent.parent.parent

CONN = os.environ.get(
    "SDLC_DB_CONNECTION",
    "Host=localhost;Port=5432;Database=platforma_sdlc;Username=platforma;Password=platforma_dev_password",
)


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
        return psycopg2.connect(**parse_conn(CONN))
    except ImportError:
        print("[insert_model_run] psycopg2 미설치 — pip install psycopg2-binary", file=sys.stderr)
        return None
    except Exception as e:
        print(f"[insert_model_run] PostgreSQL 연결 실패: {e}", file=sys.stderr)
        return None


def call_count_tokens(created_at: str) -> tuple:
    """count_tokens.py를 호출하여 (duration_sec, consume_tokens, cache_tokens) 반환."""
    script = Path(__file__).parent / "count_tokens.py"
    for python in ("python", "python3"):
        try:
            result = subprocess.run(
                [python, str(script), created_at],
                capture_output=True, text=True, timeout=30
            )
            if result.returncode == 0 and result.stdout.strip():
                data = {}
                for line in result.stdout.strip().splitlines():
                    if "=" in line:
                        k, v = line.split("=", 1)
                        data[k.strip()] = v.strip()
                try:
                    return (
                        int(data.get("duration_sec", 0)),
                        int(data.get("consume_tokens", 0)),
                        int(data.get("cache_tokens", 0)),
                    )
                except ValueError:
                    pass
        except Exception:
            continue
    return (0, 0, 0)


def lookup_job_id(cur, branch: str) -> Optional[int]:
    """sdlc.ai_jobs에서 branch로 id 조회."""
    try:
        cur.execute("SELECT id FROM sdlc.ai_jobs WHERE branch = %s LIMIT 1", (branch,))
        row = cur.fetchone()
        return row[0] if row else None
    except Exception as e:
        print(f"[insert_model_run] ai_jobs 조회 실패: {e}", file=sys.stderr)
        return None


def insert_model_run(branch: str, created_at: str) -> bool:
    conn = get_conn()
    if conn is None:
        return False

    try:
        duration_sec, consume_tokens, cache_tokens = call_count_tokens(created_at)

        raw_usage = json.dumps({
            "duration_sec": duration_sec,
            "consume_tokens": consume_tokens,
            "cache_tokens": cache_tokens,
        })

        now = datetime.now(timezone.utc)

        # created_at 파싱
        try:
            started_at = datetime.fromisoformat(created_at.replace("Z", "+00:00"))
        except Exception:
            started_at = now

        with conn:
            cur = conn.cursor()
            job_id = lookup_job_id(cur, branch)

            cur.execute(
                """
                INSERT INTO sdlc.ai_model_runs
                    (job_id, model_name, provider,
                     total_tokens, cache_read_tokens,
                     started_at, completed_at, raw_usage, created_at)
                VALUES
                    (%s, %s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (
                    job_id,
                    "claude-sonnet-4-6",
                    "anthropic",
                    consume_tokens if consume_tokens > 0 else None,
                    cache_tokens if cache_tokens > 0 else None,
                    started_at,
                    now,
                    raw_usage,
                    now,
                ),
            )

        print(
            f"[insert_model_run] OK — branch={branch}, "
            f"job_id={job_id}, consume={consume_tokens}, cache={cache_tokens}"
        )
        return True

    except Exception as e:
        print(f"[insert_model_run] INSERT 실패: {e}", file=sys.stderr)
        return False
    finally:
        conn.close()


def main() -> None:
    parser = argparse.ArgumentParser(description="ai_model_runs INSERT")
    parser.add_argument("--branch", required=True, help="현재 브랜치명")
    parser.add_argument("--created-at", required=True, dest="created_at", help="task JSON created_at (ISO8601)")
    args = parser.parse_args()

    insert_model_run(args.branch, args.created_at)


if __name__ == "__main__":
    main()
