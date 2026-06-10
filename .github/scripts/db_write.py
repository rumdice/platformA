#!/usr/bin/env python3
"""
SDLC PostgreSQL 쓰기/읽기 헬퍼.

액션:
  upsert-job      : ai_jobs INSERT ON CONFLICT(branch) DO UPDATE
  insert-step     : ai_job_steps INSERT ON CONFLICT(job_id, step_name) DO UPDATE (멱등)
  get-gates       : 게이트 검사 필드 조회 (stdout key=value)
  get-sprint-num  : 다음 스프린트 번호 발급 (nextval — 충돌 없음)
  list-active     : 진행 중(status != done) 작업 목록 출력

사용법:
  python db_write.py --action upsert-job   --branch <브랜치> [--sprint N] [--task 이름] [--status 상태]
  python db_write.py --action insert-step  --branch <브랜치> --step-name <이름> [--step-status done] [--step-summary "..."]
  python db_write.py --action get-gates    --branch <브랜치>
  python db_write.py --action get-sprint-num
  python db_write.py --action list-active

Phase C (DB 단독): DB 연결 실패 시 stderr 경고 후 exit 1 (graceful skip 없음).
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
        safe_error = str(error)[:200]
        line = f"[{ts}] action={action} branch={branch} error={safe_error}\n"
        with open(log_file, "a", encoding="utf-8") as f:
            f.write(line)
    except Exception:
        pass


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
    cur.execute("SELECT id FROM sdlc.ai_jobs WHERE branch = %s LIMIT 1", (branch,))
    row = cur.fetchone()
    return row[0] if row else None


def action_upsert_job(args) -> bool:
    """
    branch 기준으로 ai_jobs를 INSERT(신규) 또는 UPDATE(기존).
    행 존재 여부를 먼저 SELECT FOR UPDATE로 확인하여:
    - 신규: INSERT (sprint, task_name 필수)
    - 기존: UPDATE (제공된 필드만 덮어씀, NULL은 기존값 유지)
    FOR UPDATE로 동일 branch 동시 업데이트를 직렬화한다.
    """
    conn = get_conn()
    if conn is None:
        _log_failure("upsert-job", args.branch or "", "connection failed")
        return False
    try:
        now = datetime.now(timezone.utc)
        created_at = now
        if args.created_at:
            try:
                created_at = datetime.fromisoformat(args.created_at.replace("Z", "+00:00"))
            except Exception:
                pass
        completed_at = None
        if args.completed_at:
            try:
                completed_at = datetime.fromisoformat(args.completed_at.replace("Z", "+00:00"))
            except Exception:
                pass

        test_generated = bool(getattr(args, "test_generated", False))
        review_completed = bool(getattr(args, "review_completed", False))
        adr_required = bool(getattr(args, "adr_required", False))
        retry_count = int(getattr(args, "retry_count", 0) or 0)
        pr_url = getattr(args, "pr_url", None) or None
        consume_tokens = _int_or_none(getattr(args, "consume_tokens", None))
        cache_tokens = _int_or_none(getattr(args, "cache_tokens", None))
        duration_sec = _int_or_none(getattr(args, "duration_sec", None))
        last_error = getattr(args, "last_error", None) or None
        status = args.status or "analyzing"

        with conn:
            cur = conn.cursor()
            # 기존 행 잠금 (FOR UPDATE → 동일 branch 동시 UPDATE 직렬화)
            cur.execute(
                "SELECT id FROM sdlc.ai_jobs WHERE branch = %s FOR UPDATE",
                (args.branch,),
            )
            existing = cur.fetchone()

            if existing is None:
                # 신규 INSERT — sprint와 task_name이 없으면 fallback 값 사용
                sprint_val = int(args.sprint) if args.sprint else None
                task_val = args.task or args.branch  # branch명을 task_name 대체값으로 사용
                cur.execute(
                    """
                    INSERT INTO sdlc.ai_jobs
                        (branch, sprint, task_name, status,
                         test_generated, review_completed, adr_required, retry_count,
                         pr_url, consume_tokens, cache_tokens, duration_sec, last_error,
                         created_at, updated_at)
                    VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                    """,
                    (
                        args.branch, sprint_val, task_val, status,
                        test_generated, review_completed, adr_required, retry_count,
                        pr_url, consume_tokens, cache_tokens, duration_sec, last_error,
                        created_at, now,
                    ),
                )
            else:
                # 기존 UPDATE — NULL 값은 기존값 유지 (COALESCE)
                # test_generated / review_completed: 한번 True가 되면 False로 되돌리지 않음
                cur.execute(
                    """
                    UPDATE sdlc.ai_jobs SET
                        sprint          = COALESCE(%s, sprint),
                        task_name       = COALESCE(%s, task_name),
                        status          = %s,
                        test_generated  = CASE WHEN %s THEN TRUE ELSE test_generated END,
                        review_completed= CASE WHEN %s THEN TRUE ELSE review_completed END,
                        adr_required    = %s,
                        retry_count     = %s,
                        pr_url          = COALESCE(%s, pr_url),
                        consume_tokens  = COALESCE(%s, consume_tokens),
                        cache_tokens    = COALESCE(%s, cache_tokens),
                        duration_sec    = COALESCE(%s, duration_sec),
                        last_error      = COALESCE(%s, last_error),
                        updated_at      = %s
                    WHERE branch = %s
                    """,
                    (
                        int(args.sprint) if args.sprint else None,
                        args.task or None,
                        status,
                        test_generated, review_completed, adr_required, retry_count,
                        pr_url, consume_tokens, cache_tokens, duration_sec, last_error,
                        now, args.branch,
                    ),
                )

            # completed_at: status=done이고 아직 기록 안 된 경우만 설정
            if completed_at or status == "done":
                _completed = completed_at or now
                cur.execute(
                    "UPDATE sdlc.ai_jobs SET completed_at = %s WHERE branch = %s AND completed_at IS NULL",
                    (_completed, args.branch),
                )

        print(f"[db_write] upsert-job OK - branch={args.branch} status={status}")
        return True
    except Exception as e:
        print(f"[db_write] upsert-job 실패: {e}", file=sys.stderr)
        _log_failure("upsert-job", args.branch or "", str(e))
        return False
    finally:
        conn.close()


def action_insert_step(args) -> bool:
    conn = get_conn()
    if conn is None:
        _log_failure("insert-step", args.branch or "", "connection failed")
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
                ON CONFLICT (job_id, step_name) DO UPDATE SET
                    status       = EXCLUDED.status,
                    summary      = EXCLUDED.summary,
                    completed_at = EXCLUDED.completed_at
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
        _log_failure("insert-step", args.branch or "", str(e))
        return False
    finally:
        conn.close()


def action_get_gates(args) -> bool:
    conn = get_conn()
    if conn is None:
        _log_failure("get-gates", args.branch or "", "connection failed")
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
        _log_failure("get-gates", args.branch or "", str(e))
        return False
    finally:
        conn.close()


def action_get_sprint_num(_args) -> bool:
    """sdlc.sprint_seq에서 다음 스프린트 번호를 발급한다 (충돌 없음, 원자적)."""
    conn = get_conn()
    if conn is None:
        _log_failure("get-sprint-num", "", "connection failed")
        return False
    try:
        with conn:
            cur = conn.cursor()
            cur.execute("SELECT nextval('sdlc.sprint_seq')")
            num = cur.fetchone()[0]
        print(num)
        return True
    except Exception as e:
        print(f"[db_write] get-sprint-num 실패: {e}", file=sys.stderr)
        _log_failure("get-sprint-num", "", str(e))
        return False
    finally:
        conn.close()


def action_list_active(_args) -> bool:
    """진행 중(status != done) 작업 목록을 출력한다."""
    conn = get_conn()
    if conn is None:
        return False
    try:
        with conn:
            cur = conn.cursor()
            cur.execute(
                """
                SELECT sprint, branch, task_name, status, updated_at
                FROM sdlc.ai_jobs
                WHERE status != 'done'
                ORDER BY sprint DESC, updated_at DESC
                LIMIT 30
                """
            )
            rows = cur.fetchall()
            if not rows:
                print("active_jobs=0")
                return True
            print(f"active_jobs={len(rows)}")
            for sprint, branch, task, status, updated_at in rows:
                ts = updated_at.strftime("%Y-%m-%dT%H:%M") if updated_at else "?"
                print(f"  sprint={sprint} status={status} branch={branch} task={task} updated={ts}")
        return True
    except Exception as e:
        print(f"[db_write] list-active 실패: {e}", file=sys.stderr)
        return False
    finally:
        conn.close()


def _int_or_none(val) -> Optional[int]:
    if val is None:
        return None
    try:
        return int(val)
    except (TypeError, ValueError):
        return None


def main() -> None:
    parser = argparse.ArgumentParser(description="SDLC PostgreSQL 쓰기/읽기 헬퍼")
    parser.add_argument("--action", required=True,
                        choices=["upsert-job", "insert-step", "get-gates", "get-sprint-num", "list-active"])
    parser.add_argument("--branch")
    # upsert-job
    parser.add_argument("--sprint")
    parser.add_argument("--task")
    parser.add_argument("--status")
    parser.add_argument("--created-at", dest="created_at")
    parser.add_argument("--completed-at", dest="completed_at")
    parser.add_argument("--test-generated", dest="test_generated", action="store_true", default=False)
    parser.add_argument("--review-completed", dest="review_completed", action="store_true", default=False)
    parser.add_argument("--adr-required", dest="adr_required", action="store_true", default=False)
    parser.add_argument("--retry-count", dest="retry_count", type=int, default=0)
    parser.add_argument("--pr-url", dest="pr_url")
    parser.add_argument("--consume-tokens", dest="consume_tokens", type=int)
    parser.add_argument("--cache-tokens", dest="cache_tokens", type=int)
    parser.add_argument("--duration-sec", dest="duration_sec", type=int)
    parser.add_argument("--last-error", dest="last_error")
    # insert-step
    parser.add_argument("--step-name", dest="step_name")
    parser.add_argument("--step-status", dest="step_status", default="done")
    parser.add_argument("--step-summary", dest="step_summary", default="")
    parser.add_argument("--started-at", dest="started_at")
    args = parser.parse_args()

    actions_requiring_branch = {"upsert-job", "insert-step", "get-gates"}
    if args.action in actions_requiring_branch and not args.branch:
        parser.error(f"--branch is required for --action {args.action}")

    ok = False
    if args.action == "upsert-job":
        ok = action_upsert_job(args)
    elif args.action == "insert-step":
        ok = action_insert_step(args)
    elif args.action == "get-gates":
        ok = action_get_gates(args)
    elif args.action == "get-sprint-num":
        ok = action_get_sprint_num(args)
    elif args.action == "list-active":
        ok = action_list_active(args)

    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
