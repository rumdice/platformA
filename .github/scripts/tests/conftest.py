"""
공통 fixture — AI_SDLC Python 통합 테스트
DB 연결: SDLC_DB_CONNECTION 환경변수 또는 localhost 기본값
"""
import os
import uuid
from datetime import datetime, timezone

import pytest

CONN_STR = os.environ.get(
    "SDLC_DB_CONNECTION",
    "Host=localhost;Port=5432;Database=platforma_sdlc;Username=platforma;Password=platforma_dev_password",
)


def _parse_conn(conn_str: str) -> dict:
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


@pytest.fixture(scope="session")
def db_conn():
    """세션 단위 DB 연결. psycopg2 미설치 또는 연결 실패 시 skip."""
    try:
        import psycopg2
    except ImportError:
        pytest.skip("psycopg2 미설치 — pip install psycopg2-binary")

    try:
        conn = psycopg2.connect(**_parse_conn(CONN_STR))
        conn.autocommit = False
        yield conn
        conn.close()
    except Exception as e:
        pytest.skip(f"PostgreSQL 연결 실패: {e}")


@pytest.fixture
def test_branch(db_conn):
    """테스트용 더미 branch를 ai_jobs에 INSERT하고, 테스트 후 DELETE한다."""
    import psycopg2

    branch = f"test-jl-{uuid.uuid4().hex[:8]}"
    now = datetime.now(timezone.utc)
    cur = db_conn.cursor()
    cur.execute(
        """
        INSERT INTO sdlc.ai_jobs
            (branch, sprint, task, status, owner, created_at, updated_at)
        VALUES (%s, 9999, 'test-task', 'analyzing', 'pytest', %s, %s)
        """,
        (branch, now, now),
    )
    db_conn.commit()
    yield branch
    cur.execute("DELETE FROM sdlc.ai_jobs WHERE branch = %s", (branch,))
    db_conn.commit()
    cur.close()
