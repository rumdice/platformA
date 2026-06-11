"""
job_lock.py 통합 테스트 — 8개 시나리오
실행: pytest .github/scripts/tests/test_job_lock.py -m integration -v
전제: PostgreSQL platforma_sdlc DB 접근 가능, sdlc.ai_jobs 테이블 존재
"""
import subprocess
import sys
import uuid
from datetime import datetime, timedelta, timezone
from pathlib import Path

import pytest

pytestmark = pytest.mark.integration

SCRIPT = str(Path(__file__).resolve().parent.parent / "job_lock.py")


def _run(args: list[str]) -> tuple[int, str]:
    """job_lock.py CLI 실행 후 (exit_code, stdout+stderr) 반환."""
    result = subprocess.run(
        [sys.executable, SCRIPT] + args,
        capture_output=True,
        text=True,
        timeout=15,
    )
    output = result.stdout + result.stderr
    return result.returncode, output


def test_claim_unlocked_job(test_branch):
    """미잠금 job에 claim 성공 → exit 0, LOCK_TOKEN 출력."""
    code, out = _run(["claim", "--branch", test_branch, "--owner", "pytest"])
    assert code == 0, f"expected exit 0, got {code}. output: {out}"
    assert "LOCK_TOKEN=" in out

    # cleanup: release
    token = next(l.split("=", 1)[1] for l in out.splitlines() if l.startswith("LOCK_TOKEN="))
    _run(["release", "--branch", test_branch, "--token", token])


def test_claim_nonexistent_branch():
    """존재하지 않는 branch → exit 2."""
    fake = f"nonexistent-{uuid.uuid4().hex[:6]}"
    code, out = _run(["claim", "--branch", fake])
    assert code == 2, f"expected exit 2, got {code}. output: {out}"


def test_claim_already_locked_by_other(test_branch):
    """다른 owner가 점유 중 → exit 1."""
    code1, out1 = _run(["claim", "--branch", test_branch, "--owner", "owner-a", "--ttl", "60"])
    assert code1 == 0

    token = next(l.split("=", 1)[1] for l in out1.splitlines() if l.startswith("LOCK_TOKEN="))

    code2, out2 = _run(["claim", "--branch", test_branch, "--owner", "owner-b"])
    assert code2 == 1, f"expected exit 1, got {code2}. output: {out2}"

    # cleanup
    _run(["release", "--branch", test_branch, "--token", token])


def test_claim_same_owner_reentry(test_branch):
    """동일 owner 재진입 → exit 0 (token 갱신)."""
    code1, out1 = _run(["claim", "--branch", test_branch, "--owner", "pytest"])
    assert code1 == 0
    token1 = next(l.split("=", 1)[1] for l in out1.splitlines() if l.startswith("LOCK_TOKEN="))

    code2, out2 = _run(["claim", "--branch", test_branch, "--owner", "pytest"])
    assert code2 == 0, f"expected exit 0 on reentry, got {code2}"
    token2 = next(l.split("=", 1)[1] for l in out2.splitlines() if l.startswith("LOCK_TOKEN="))

    assert token1 != token2, "재진입 시 token이 갱신되어야 한다"

    _run(["release", "--branch", test_branch, "--token", token2])


def test_claim_stale_lock(db_conn, test_branch):
    """TTL 만료된 lock은 다른 owner도 claim 가능."""
    import psycopg2

    now = datetime.now(timezone.utc)
    expired = now - timedelta(minutes=5)
    cur = db_conn.cursor()
    cur.execute(
        """
        UPDATE sdlc.ai_jobs
        SET locked_by='stale-owner', locked_at=%s, lock_expires_at=%s,
            lock_token=%s, agent_id='test', last_heartbeat_at=%s, updated_at=%s
        WHERE branch=%s
        """,
        (expired, expired, str(uuid.uuid4()), expired, now, test_branch),
    )
    db_conn.commit()
    cur.close()

    code, out = _run(["claim", "--branch", test_branch, "--owner", "new-owner"])
    assert code == 0, f"만료된 lock은 새 owner가 획득 가능해야 함. exit={code}, out={out}"

    token = next(l.split("=", 1)[1] for l in out.splitlines() if l.startswith("LOCK_TOKEN="))
    _run(["release", "--branch", test_branch, "--token", token])


def test_release_wrong_token(test_branch):
    """잘못된 token으로 release → exit 1."""
    code1, out1 = _run(["claim", "--branch", test_branch, "--owner", "pytest"])
    assert code1 == 0
    real_token = next(l.split("=", 1)[1] for l in out1.splitlines() if l.startswith("LOCK_TOKEN="))

    code2, _ = _run(["release", "--branch", test_branch, "--token", str(uuid.uuid4())])
    assert code2 == 1, "잘못된 token으로는 release 불가"

    # cleanup with real token
    _run(["release", "--branch", test_branch, "--token", real_token])


def test_heartbeat_extends_ttl(db_conn, test_branch):
    """heartbeat 후 lock_expires_at이 갱신된다."""
    code, out = _run(["claim", "--branch", test_branch, "--owner", "pytest", "--ttl", "1"])
    assert code == 0
    token = next(l.split("=", 1)[1] for l in out.splitlines() if l.startswith("LOCK_TOKEN="))

    cur = db_conn.cursor()
    cur.execute("SELECT lock_expires_at FROM sdlc.ai_jobs WHERE branch=%s", (test_branch,))
    expires_before = cur.fetchone()[0]

    code2, _ = _run(["heartbeat", "--branch", test_branch, "--token", token, "--ttl", "60"])
    assert code2 == 0

    cur.execute("SELECT lock_expires_at FROM sdlc.ai_jobs WHERE branch=%s", (test_branch,))
    expires_after = cur.fetchone()[0]
    cur.close()

    assert expires_after > expires_before, "heartbeat 후 만료 시각이 연장되어야 한다"

    _run(["release", "--branch", test_branch, "--token", token])


def test_expire_clears_stale_locks(db_conn, test_branch):
    """expire 명령으로 만료된 lock이 해제된다."""
    now = datetime.now(timezone.utc)
    expired = now - timedelta(minutes=10)
    cur = db_conn.cursor()
    cur.execute(
        """
        UPDATE sdlc.ai_jobs
        SET locked_by='stale', locked_at=%s, lock_expires_at=%s,
            lock_token=%s, agent_id='test', last_heartbeat_at=%s, updated_at=%s
        WHERE branch=%s
        """,
        (expired, expired, str(uuid.uuid4()), expired, now, test_branch),
    )
    db_conn.commit()
    cur.close()

    code, out = _run(["expire"])
    assert code == 0
    assert test_branch in out or "stale lock" in out.lower() or "released" in out.lower()

    cur2 = db_conn.cursor()
    cur2.execute("SELECT locked_by FROM sdlc.ai_jobs WHERE branch=%s", (test_branch,))
    row = cur2.fetchone()
    cur2.close()
    assert row[0] is None, "expire 후 locked_by가 NULL이어야 한다"
