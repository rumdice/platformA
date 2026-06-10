#!/usr/bin/env python3
"""
AI_SDLC Job Lock 관리.

동일 branch/job에 대한 다중 agent 동시 실행을 PostgreSQL row-level atomic UPDATE로 방지한다.

사용법:
  python job_lock.py claim     --branch BRANCH [--ttl 60] [--owner NAME] [--agent-id ID]
  python job_lock.py release   --branch BRANCH --token TOKEN
  python job_lock.py heartbeat --branch BRANCH --token TOKEN [--ttl 60]
  python job_lock.py status    --branch BRANCH
  python job_lock.py expire
  python job_lock.py list-active

exit code:
  0 = 성공
  1 = lock 획득 실패 (다른 agent가 점유 중) / release token 불일치
  2 = job 없음
  3 = DB 연결 실패
"""

import argparse
import os
import subprocess
import sys
import uuid
from datetime import datetime, timedelta, timezone
from pathlib import Path

CONN_STR = os.environ.get(
    "SDLC_DB_CONNECTION",
    "Host=localhost;Port=5432;Database=platforma_sdlc;Username=platforma;Password=platforma_dev_password",
)

_REPO_ROOT = Path(__file__).resolve().parent.parent.parent
_LOCK_FILE = _REPO_ROOT / ".ai_sdlc_lock"

DEFAULT_TTL_MINUTES = 60


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
        return psycopg2.connect(**parse_conn(CONN_STR))
    except ImportError:
        print("[job_lock] psycopg2 미설치 - pip install psycopg2-binary", file=sys.stderr)
        return None
    except Exception as e:
        print(f"[job_lock] PostgreSQL 연결 실패: {e}", file=sys.stderr)
        return None


def _get_git_owner() -> str:
    for key in ("user.name", "user.email"):
        try:
            result = subprocess.run(
                ["git", "config", key],
                capture_output=True, text=True, timeout=3,
            )
            val = result.stdout.strip()
            if val:
                return val
        except Exception:
            pass
    return "unknown"


def cmd_claim(args) -> int:
    conn = get_conn()
    if conn is None:
        return 3

    owner = args.owner or _get_git_owner()
    agent_id = args.agent_id or "claude-code"
    ttl = args.ttl or DEFAULT_TTL_MINUTES
    token = str(uuid.uuid4())
    now = datetime.now(timezone.utc)

    try:
        with conn:
            cur = conn.cursor()

            # job 존재 확인 + 현재 lock 상태 조회 (FOR UPDATE로 직렬화)
            cur.execute(
                "SELECT id, locked_by, lock_expires_at FROM sdlc.ai_jobs WHERE branch = %s FOR UPDATE",
                (args.branch,),
            )
            row = cur.fetchone()
            if row is None:
                print(f"[job_lock] ❌ job 없음: {args.branch}", file=sys.stderr)
                return 2

            _job_id, current_locked_by, current_expires = row

            # atomic claim: 만료됐거나 lock 없거나 같은 owner면 획득 가능
            cur.execute(
                """
                UPDATE sdlc.ai_jobs
                SET locked_by         = %(owner)s,
                    locked_at         = %(now)s,
                    lock_expires_at   = %(expires)s,
                    lock_token        = %(token)s,
                    agent_id          = %(agent_id)s,
                    last_heartbeat_at = %(now)s,
                    updated_at        = %(now)s
                WHERE branch = %(branch)s
                  AND (
                      lock_expires_at IS NULL
                      OR lock_expires_at < %(now)s
                      OR locked_by = %(owner)s
                  )
                RETURNING id
                """,
                {
                    "owner": owner,
                    "now": now,
                    "expires": now + timedelta(minutes=ttl),
                    "token": token,
                    "agent_id": agent_id,
                    "branch": args.branch,
                },
            )
            updated = cur.fetchone()
            if updated is None:
                expires_str = (
                    current_expires.strftime("%Y-%m-%dT%H:%M UTC") if current_expires else "?"
                )
                print(
                    f"[job_lock] lock claim failed\n"
                    f"  current owner={current_locked_by}  expires={expires_str}\n"
                    f"  wait or retry after expiry.\n"
                    f"  force expire: python .github/scripts/job_lock.py expire",
                    file=sys.stderr,
                )
                return 1

        # 성공: token 파일 저장 + stdout 출력
        _LOCK_FILE.write_text(f"branch={args.branch}\ntoken={token}\n", encoding="utf-8")
        print(f"LOCK_TOKEN={token}")
        print(f"[job_lock] lock claimed: branch={args.branch} owner={owner} ttl={ttl}m")
        return 0
    except Exception as e:
        print(f"[job_lock] claim 오류: {e}", file=sys.stderr)
        return 3
    finally:
        conn.close()


def cmd_release(args) -> int:
    conn = get_conn()
    if conn is None:
        return 3

    try:
        with conn:
            cur = conn.cursor()
            cur.execute(
                """
                UPDATE sdlc.ai_jobs
                SET locked_by         = NULL,
                    locked_at         = NULL,
                    lock_expires_at   = NULL,
                    lock_token        = NULL,
                    agent_id          = NULL,
                    last_heartbeat_at = NULL,
                    updated_at        = NOW()
                WHERE branch = %(branch)s
                  AND lock_token = %(token)s
                RETURNING id
                """,
                {"branch": args.branch, "token": args.token},
            )
            updated = cur.fetchone()
            if updated is None:
                print(
                    f"[job_lock] release failed: token mismatch or already released (branch={args.branch})",
                    file=sys.stderr,
                )
                return 1

        _LOCK_FILE.unlink(missing_ok=True)
        print(f"[job_lock] lock released: branch={args.branch}")
        return 0
    except Exception as e:
        print(f"[job_lock] release 오류: {e}", file=sys.stderr)
        return 3
    finally:
        conn.close()


def cmd_heartbeat(args) -> int:
    conn = get_conn()
    if conn is None:
        return 3

    ttl = args.ttl or DEFAULT_TTL_MINUTES
    now = datetime.now(timezone.utc)

    try:
        with conn:
            cur = conn.cursor()
            cur.execute(
                """
                UPDATE sdlc.ai_jobs
                SET lock_expires_at   = %(expires)s,
                    last_heartbeat_at = %(now)s,
                    updated_at        = %(now)s
                WHERE branch = %(branch)s
                  AND lock_token = %(token)s
                RETURNING id
                """,
                {
                    "expires": now + timedelta(minutes=ttl),
                    "now": now,
                    "branch": args.branch,
                    "token": args.token,
                },
            )
            updated = cur.fetchone()
            if updated is None:
                print(
                    f"[job_lock] heartbeat failed: token mismatch or no lock",
                    file=sys.stderr,
                )
                return 1

        print(f"[job_lock] heartbeat ok: branch={args.branch} ttl+{ttl}m")
        return 0
    except Exception as e:
        print(f"[job_lock] heartbeat 오류: {e}", file=sys.stderr)
        return 3
    finally:
        conn.close()


def cmd_status(args) -> int:
    conn = get_conn()
    if conn is None:
        return 3

    try:
        with conn:
            cur = conn.cursor()
            cur.execute(
                """
                SELECT locked_by, locked_at, lock_expires_at, agent_id, last_heartbeat_at
                FROM sdlc.ai_jobs
                WHERE branch = %s
                """,
                (args.branch,),
            )
            row = cur.fetchone()
            if row is None:
                print(f"[job_lock] job 없음: {args.branch}")
                return 2

            locked_by, _locked_at, expires, agent_id, heartbeat = row
            now = datetime.now(timezone.utc)

            if locked_by is None:
                print(f"status=unlocked  branch={args.branch}")
            else:
                is_expired = expires and expires < now
                state = "STALE(expired)" if is_expired else "locked"
                exp_str = expires.strftime("%Y-%m-%dT%H:%M UTC") if expires else "?"
                hb_str = heartbeat.strftime("%Y-%m-%dT%H:%M UTC") if heartbeat else "?"
                print(
                    f"status={state}  branch={args.branch}\n"
                    f"  owner={locked_by}  agent={agent_id or '?'}\n"
                    f"  expires={exp_str}  last_heartbeat={hb_str}"
                )
        return 0
    except Exception as e:
        print(f"[job_lock] status 오류: {e}", file=sys.stderr)
        return 3
    finally:
        conn.close()


def cmd_expire(_args) -> int:
    conn = get_conn()
    if conn is None:
        return 3

    try:
        with conn:
            cur = conn.cursor()
            cur.execute(
                """
                UPDATE sdlc.ai_jobs
                SET locked_by         = NULL,
                    locked_at         = NULL,
                    lock_expires_at   = NULL,
                    lock_token        = NULL,
                    agent_id          = NULL,
                    last_heartbeat_at = NULL,
                    updated_at        = NOW()
                WHERE lock_expires_at < NOW()
                RETURNING branch
                """,
            )
            expired = cur.fetchall()
            if expired:
                print(f"[job_lock] {len(expired)} stale lock(s) released:")
                for (branch,) in expired:
                    print(f"  {branch}")
            else:
                print("[job_lock] 만료된 lock 없음")
        return 0
    except Exception as e:
        print(f"[job_lock] expire 오류: {e}", file=sys.stderr)
        return 3
    finally:
        conn.close()


def cmd_list_active(_args) -> int:
    conn = get_conn()
    if conn is None:
        return 3

    try:
        with conn:
            cur = conn.cursor()
            cur.execute(
                """
                SELECT branch, locked_by, lock_expires_at, agent_id
                FROM sdlc.ai_jobs
                WHERE locked_by IS NOT NULL
                ORDER BY locked_at DESC NULLS LAST
                LIMIT 20
                """,
            )
            rows = cur.fetchall()
            now = datetime.now(timezone.utc)
            active, stale = [], []
            for branch, locked_by, expires, agent_id in rows:
                if expires and expires < now:
                    stale.append((branch, locked_by, expires, agent_id))
                else:
                    active.append((branch, locked_by, expires, agent_id))

            print(f"active_locks={len(active)}  stale_locks={len(stale)}")
            if active:
                print("  [Active]")
                for branch, owner, expires, agent in active:
                    exp_str = expires.strftime("%Y-%m-%dT%H:%M UTC") if expires else "?"
                    print(f"    branch={branch} owner={owner} agent={agent or '?'} expires={exp_str}")
            if stale:
                print("  [Stale — 만료됨]")
                for branch, owner, expires, agent in stale:
                    exp_str = expires.strftime("%Y-%m-%dT%H:%M UTC") if expires else "?"
                    print(f"    branch={branch} owner={owner} expired={exp_str}")
                print("  → 해제: python .github/scripts/job_lock.py expire")
        return 0
    except Exception as e:
        print(f"[job_lock] list-active 오류: {e}", file=sys.stderr)
        return 3
    finally:
        conn.close()


def main() -> None:
    parser = argparse.ArgumentParser(description="AI_SDLC Job Lock 관리")
    sub = parser.add_subparsers(dest="cmd", required=True)

    p_claim = sub.add_parser("claim", help="lock 획득")
    p_claim.add_argument("--branch", required=True)
    p_claim.add_argument("--ttl", type=int, default=DEFAULT_TTL_MINUTES, help="TTL(분), 기본 60")
    p_claim.add_argument("--owner", help="lock 소유자 (기본: git user.name)")
    p_claim.add_argument("--agent-id", dest="agent_id", help="실행 주체 (기본: claude-code)")

    p_rel = sub.add_parser("release", help="lock 해제")
    p_rel.add_argument("--branch", required=True)
    p_rel.add_argument("--token", required=True)

    p_hb = sub.add_parser("heartbeat", help="lock TTL 연장")
    p_hb.add_argument("--branch", required=True)
    p_hb.add_argument("--token", required=True)
    p_hb.add_argument("--ttl", type=int, default=DEFAULT_TTL_MINUTES)

    p_st = sub.add_parser("status", help="lock 상태 조회")
    p_st.add_argument("--branch", required=True)

    sub.add_parser("expire", help="만료된 lock 일괄 해제")
    sub.add_parser("list-active", help="현재 lock 중인 job 목록")

    args = parser.parse_args()

    dispatch = {
        "claim": cmd_claim,
        "release": cmd_release,
        "heartbeat": cmd_heartbeat,
        "status": cmd_status,
        "expire": cmd_expire,
        "list-active": cmd_list_active,
    }
    sys.exit(dispatch[args.cmd](args))


if __name__ == "__main__":
    main()
