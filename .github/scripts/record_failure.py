#!/usr/bin/env python3
"""
sdlc.ai_failures 테이블에 실패를 기록하거나 조회하는 로컬 스크립트.

PostgreSQL이 로컬에서 실행 중이어야 한다 (docker/postgresql/docker-compose.yml).
n8n도 이 스크립트를 Execute Command 노드로 호출할 수 있다.

사용법:
  # 실패 기록
  python record_failure.py --type format_failed --branch BRANCH --message "msg"

  # 미해결 실패 조회 (session-start.sh에서 사용)
  python record_failure.py --list-unresolved --branch BRANCH

  # 실패 해결 처리
  python record_failure.py --resolve --branch BRANCH --type format_failed
"""

import argparse
import json
import os
import sys
from datetime import datetime, timezone

CONN = os.environ.get(
    "SDLC_DB_CONNECTION",
    "Host=localhost;Port=5432;Database=platforma_sdlc;Username=platforma;Password=platforma_dev_password"
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
        params = parse_conn(CONN)
        return psycopg2.connect(**params)
    except ImportError:
        print("[record_failure] psycopg2 not installed. Install: pip install psycopg2-binary", file=sys.stderr)
        return None
    except Exception as e:
        print(f"[record_failure] PostgreSQL 연결 실패: {e}", file=sys.stderr)
        return None


def record(failure_type: str, branch: str, message: str, fixable: bool = False) -> bool:
    """
    ai_failures에 INSERT한다. branch는 metadata jsonb에 저장한다.
    (AiFailure 엔티티에 branch 컬럼이 없으므로 metadata 활용)
    """
    conn = get_conn()
    if conn is None:
        return False
    meta = json.dumps({"branch": branch, "source_system": "github_actions"})
    try:
        with conn.cursor() as cur:
            cur.execute("""
                INSERT INTO sdlc.ai_failures
                    (failure_type, source, message, fixable_by_ai, resolved, created_at, metadata)
                VALUES (%s, %s, %s, %s, false, %s, %s)
            """, (
                failure_type,
                "ci_github_actions",
                message,
                fixable,
                datetime.now(timezone.utc),
                meta,
            ))
        conn.commit()
        print(f"[record_failure] ✓ 기록: {failure_type} / {branch}")
        return True
    except Exception as e:
        print(f"[record_failure] INSERT 실패: {e}", file=sys.stderr)
        return False
    finally:
        conn.close()


def list_unresolved(branch: str) -> list[dict]:
    """metadata->>'branch' 로 브랜치별 미해결 실패를 조회한다."""
    conn = get_conn()
    if conn is None:
        return []
    try:
        with conn.cursor() as cur:
            if branch:
                cur.execute("""
                    SELECT failure_type, message, created_at, metadata
                    FROM sdlc.ai_failures
                    WHERE resolved = false AND (metadata::jsonb)->>'branch' = %s
                    ORDER BY created_at DESC LIMIT 5
                """, (branch,))
            else:
                cur.execute("""
                    SELECT failure_type, (metadata::jsonb)->>'branch', message, created_at
                    FROM sdlc.ai_failures
                    WHERE resolved = false
                    ORDER BY created_at DESC LIMIT 10
                """)
            rows = cur.fetchall()
            return [{"failure_type": r[0], "branch": branch or r[1],
                     "message": str(r[2])[:120], "created_at": str(r[3])} for r in rows]
    except Exception as e:
        print(f"[record_failure] SELECT 실패: {e}", file=sys.stderr)
        return []
    finally:
        conn.close()


def resolve(branch: str, failure_type: str) -> bool:
    """metadata->>'branch' 기준으로 해결 처리한다."""
    conn = get_conn()
    if conn is None:
        return False
    try:
        with conn.cursor() as cur:
            cur.execute("""
                UPDATE sdlc.ai_failures
                SET resolved = true, resolved_at = %s
                WHERE metadata->>'branch' = %s::text AND failure_type = %s AND resolved = false
            """, (datetime.now(timezone.utc), branch, failure_type))
        conn.commit()
        print(f"[record_failure] ✓ 해결 처리: {failure_type} / {branch}")
        return True
    except Exception as e:
        print(f"[record_failure] UPDATE 실패: {e}", file=sys.stderr)
        return False
    finally:
        conn.close()


def main() -> None:
    parser = argparse.ArgumentParser(description="sdlc.ai_failures 관리")
    parser.add_argument("--type", dest="failure_type", help="실패 유형")
    parser.add_argument("--branch", default="", help="브랜치명")
    parser.add_argument("--message", default="", help="실패 메시지")
    parser.add_argument("--fixable", action="store_true", help="AI 자동 수정 가능 여부")
    parser.add_argument("--list-unresolved", action="store_true", help="미해결 실패 조회")
    parser.add_argument("--resolve", action="store_true", help="실패 해결 처리")
    parser.add_argument("--json", action="store_true", help="JSON 출력")
    args = parser.parse_args()

    if args.list_unresolved:
        failures = list_unresolved(args.branch)
        if args.json:
            print(json.dumps(failures, ensure_ascii=False, default=str))
        elif failures:
            print(f"\n[미해결 CI 실패 — {args.branch or '전체'}]")
            for f in failures:
                ts = str(f["created_at"])[:10]
                print(f"  [{ts}] {f['failure_type']}: {str(f['message'])[:80]}")
        else:
            print("[record_failure] 미해결 실패 없음")
    elif args.resolve:
        resolve(args.branch, args.failure_type or "")
    elif args.failure_type:
        record(args.failure_type, args.branch, args.message, args.fixable)
    else:
        parser.print_help()


if __name__ == "__main__":
    main()
