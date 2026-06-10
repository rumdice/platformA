"""
sdlc_db_migrations.sql 멱등성 + Migration 004 컬럼 존재 검증
실행: pytest .github/scripts/tests/test_migration_idempotency.py -m integration -v
"""
import os
from pathlib import Path

import pytest

pytestmark = pytest.mark.integration

SQL_FILE = Path(__file__).resolve().parent.parent / "sdlc_db_migrations.sql"

MIGRATION_004_COLUMNS = [
    "locked_by",
    "locked_at",
    "lock_expires_at",
    "lock_token",
    "agent_id",
    "last_heartbeat_at",
]


def test_migration_sql_file_exists():
    """sdlc_db_migrations.sql 파일이 존재해야 한다."""
    assert SQL_FILE.exists(), f"Migration 파일 없음: {SQL_FILE}"


def test_migration_004_columns_exist(db_conn):
    """Migration 004에서 추가된 lock 컬럼 6개가 ai_jobs 테이블에 존재해야 한다."""
    cur = db_conn.cursor()
    cur.execute(
        """
        SELECT column_name
        FROM information_schema.columns
        WHERE table_schema = 'sdlc'
          AND table_name   = 'ai_jobs'
          AND column_name  = ANY(%s)
        """,
        (MIGRATION_004_COLUMNS,),
    )
    found = {row[0] for row in cur.fetchall()}
    cur.close()

    missing = set(MIGRATION_004_COLUMNS) - found
    assert not missing, f"Migration 004 컬럼 누락: {missing}"


def test_migration_idempotency(db_conn):
    """sdlc_db_migrations.sql을 재실행해도 오류가 없어야 한다 (IF NOT EXISTS 보장)."""
    import psycopg2

    sql = SQL_FILE.read_text(encoding="utf-8")

    cur = db_conn.cursor()
    try:
        cur.execute(sql)
        db_conn.commit()
    except psycopg2.Error as e:
        db_conn.rollback()
        pytest.fail(f"Migration 재실행 실패 (멱등성 위반): {e}")
    finally:
        cur.close()


def test_migration_004_index_exists(db_conn):
    """Migration 004 인덱스(ix_ai_jobs_lock_expires)가 존재해야 한다."""
    cur = db_conn.cursor()
    cur.execute(
        """
        SELECT indexname
        FROM pg_indexes
        WHERE schemaname = 'sdlc'
          AND tablename  = 'ai_jobs'
          AND indexname  = 'ix_ai_jobs_lock_expires'
        """
    )
    row = cur.fetchone()
    cur.close()
    assert row is not None, "ix_ai_jobs_lock_expires 인덱스가 없음"


def test_migration_down_sql_documented():
    """각 Migration 블록에 Down(rollback) 주석이 포함되어야 한다."""
    sql = SQL_FILE.read_text(encoding="utf-8")
    assert "Migration 004 DOWN" in sql, \
        "sdlc_db_migrations.sql에 'Migration 004 DOWN' 주석이 없음"
