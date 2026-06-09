#!/usr/bin/env python3
"""
SDLC PostgreSQL 쓰기/읽기 헬퍼.

액션:
  upsert-job   : ai_jobs INSERT ON CONFLICT(branch) DO UPDATE
  insert-step  : ai_job_steps INSERT
  get-gates    : 게이트 검사 필드 조회 (stdout key=value)

사용법:
  python db_write.py --action upsert-job --branch <브랜치> --sprint <N> --task <이름> --status <상태>
  python db_write.py --action insert-step --branch <브랜치> --step-name <이름> --step-status done --step-summary "..."
  python db_write.py --action get-gates --branch <브랜치>

DB 연결 실패 시 stderr 경고 후 exit 0 (파일 흐름 차단 금지).
"""

import argparse
import os
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Optional

_REPO_ROOT = Path(__file__).resolve().parent.parent.parent
_FAILURE_LOG_DIR = _REPO_ROOT / "AI" / "logs" / "db-write-failures"


def _log_failure(action: str, branch: str, error: str) -> None:
    try:
        _FAILURE_LOG_DIR.mkdir(parents=True, exist_ok=True)
        today = datetime.now().strftime("%Y-%m-%d")
        log_file = _FAILURE_LOG_DIR / f"{today}.log"
        ts = datetime.now().astimezone().isoformat(timespec="seconds")
        # connection string 전체를 로그에 남기지 않음
        safe_error = str(error)[:200]
        line = f"[{ts}] action={action} branch={branch} error={safe_error}\n"
        with open(log_file, "a", encoding="utf-8") as f:
            f.write(line)
    except Exception:
        pass  # 로그 기록 실패는 무시

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
        print("[db_write] psycopg2 미설치 - pip install psycopg2-binary", file=sys.stderr)
        return None
    except Exception as e:
        print(f"[db_write] PostgreSQL 연결 실패: {e}", file=sys.stderr)
        return None


def lookup_job_id(cur, branch: str) -> Optional[int]:
    try:
        cur.execute("SELECT id FROM sdlc.ai_jobs WHERE branch = %s LIMIT 1", (branch,))
        row = cur.fetchone()
        return row[0] if row else None
    except Exception:
        return None


def action_upsert_job(args) -> bool:
    conn = get_conn()
    if conn is None:
        _log_failure("upsert-job", args.branch, "connection failed")
        return False
    try:
        now = datetime.now(timezone.utc)
        created_at = now
        if args.created_at:
            try:
                created_at = datetime.fromisoformat(args.created_at.replace("Z", "+00:00"))
            except Exception:
                pass

        test_generated = getattr(args, "test_generated", False) or False
        review_completed = getattr(args, "review_completed", False) or False
        adr_required = getattr(args, "adr_required", False) or False
        retry_count = int(getattr(args, "retry_count", 0) or 0)

        with conn:
            cur = conn.cursor()
            cur.execute(
                """
                INSERT INTO sdlc.ai_jobs
                    (branch, sprint, task_name, status,
                     test_generated, review_completed, adr_required, retry_count,
                     created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                ON CONFLICT (branch) DO UPDATE SET
                    status = EXCLUDED.status,
                    test_generated = CASE WHEN EXCLUDED.test_generated THEN TRUE ELSE sdlc.ai_jobs.test_generated END,
                    review_completed = CASE WHEN EXCLUDED.review_completed THEN TRUE ELSE sdlc.ai_jobs.review_completed END,
                    adr_required = EXCLUDED.adr_required,
                    retry_count = EXCLUDED.retry_count,
                    updated_at = EXCLUDED.updated_at
                """,
                (
                    args.branch,
                    int(args.sprint) if args.sprint else None,
                    args.task or "",
                    args.status or "analyzing",
                    test_generated,
                    review_completed,
                    adr_required,
                    retry_count,
                    created_at,
                    now,
                ),
            )
        print(f"[db_write] upsert-job OK - branch={args.branch} status={args.status}")
        return True
    except Exception as e:
        print(f"[db_write] upsert-job 실패: {e}", file=sys.stderr)
        _log_failure("upsert-job", args.branch, str(e))
        return False
    finally:
        conn.close()


def action_insert_step(args) -> bool:
    conn = get_conn()
    if conn is None:
        _log_failure("insert-step", args.branch, "connection failed")
        return False
    try:
        now = datetime.now(timezone.utc)
        started_at = now
        completed_at = now
        if args.started_at:
            try:
                started_at = datetime.fromisoformat(args.started_at.replace("Z", "+00:00"))
            except Exception:
                pass
        if args.completed_at:
            try:
                completed_at = datetime.fromisoformat(args.completed_at.replace("Z", "+00:00"))
            except Exception:
                pass

        with conn:
            cur = conn.cursor()
            job_id = lookup_job_id(cur, args.branch)
            if job_id is None:
                print(f"[db_write] insert-step: branch={args.branch} 에 해당하는 ai_jobs 행 없음", file=sys.stderr)
                return False
            cur.execute(
                """
                INSERT INTO sdlc.ai_job_steps
                    (job_id, step_name, status, summary, started_at, completed_at, created_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s)
                ON CONFLICT DO NOTHING
                """,
                (
                    job_id,
                    args.step_name or "",
                    args.step_status or "done",
                    args.step_summary or "",
                    started_at,
                    completed_at,
                    now,
                ),
            )
        print(f"[db_write] insert-step OK - job_id={job_id} name={args.step_name}")
        return True
    except Exception as e:
        print(f"[db_write] insert-step 실패: {e}", file=sys.stderr)
        _log_failure("insert-step", args.branch, str(e))
        return False
    finally:
        conn.close()


def action_get_gates(args) -> bool:
    conn = get_conn()
    if conn is None:
        _log_failure("get-gates", args.branch, "connection failed")
        return False
    try:
        with conn:
            cur = conn.cursor()
            cur.execute(
                """
                SELECT
                    j.test_generated,
                    j.review_completed,
                    (j.impact IS NOT NULL) AS impact_done,
                    j.adr_required,
                    EXISTS(
                        SELECT 1 FROM sdlc.ai_job_steps s
                        WHERE s.job_id = j.id AND s.step_name = 'requirement' AND s.status = 'done'
                    ) AS requirement_done
                FROM sdlc.ai_jobs j
                WHERE j.branch = %s
                LIMIT 1
                """,
                (args.branch,),
            )
            row = cur.fetchone()
            if row is None:
                print(f"[db_write] get-gates: branch={args.branch} 에 해당하는 행 없음", file=sys.stderr)
                return False
            test_generated, review_completed, impact_done, adr_required, requirement_done = row
            print(f"test_generated={str(test_generated).lower()}")
            print(f"review_completed={str(review_completed).lower()}")
            print(f"impact_done={str(impact_done).lower()}")
            print(f"adr_required={str(adr_required).lower()}")
            print(f"requirement_done={str(requirement_done).lower()}")
        return True
    except Exception as e:
        print(f"[db_write] get-gates 실패: {e}", file=sys.stderr)
        _log_failure("get-gates", args.branch, str(e))
        return False
    finally:
        conn.close()


def main() -> None:
    parser = argparse.ArgumentParser(description="SDLC PostgreSQL 쓰기/읽기 헬퍼")
    parser.add_argument("--action", required=True, choices=["upsert-job", "insert-step", "get-gates"])
    parser.add_argument("--branch", required=True)
    # upsert-job
    parser.add_argument("--sprint")
    parser.add_argument("--task")
    parser.add_argument("--status")
    parser.add_argument("--created-at", dest="created_at")
    parser.add_argument("--test-generated", dest="test_generated", action="store_true", default=False)
    parser.add_argument("--review-completed", dest="review_completed", action="store_true", default=False)
    parser.add_argument("--adr-required", dest="adr_required", action="store_true", default=False)
    parser.add_argument("--retry-count", dest="retry_count", type=int, default=0)
    # insert-step
    parser.add_argument("--step-name", dest="step_name")
    parser.add_argument("--step-status", dest="step_status", default="done")
    parser.add_argument("--step-summary", dest="step_summary", default="")
    parser.add_argument("--started-at", dest="started_at")
    parser.add_argument("--completed-at", dest="completed_at")
    args = parser.parse_args()

    if args.action == "upsert-job":
        action_upsert_job(args)
    elif args.action == "insert-step":
        action_insert_step(args)
    elif args.action == "get-gates":
        action_get_gates(args)


if __name__ == "__main__":
    main()
