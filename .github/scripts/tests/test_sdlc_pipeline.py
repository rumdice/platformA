"""
AI_SDLC 파이프라인 E2E smoke test — 3개 시나리오
더미 branch(test-smoke-*)로 실제 DB 파이프라인 흐름을 검증한다.
실행: pytest .github/scripts/tests/test_sdlc_pipeline.py -m integration -v
"""
import subprocess
import sys
import uuid
from datetime import datetime, timezone
from pathlib import Path

import pytest

pytestmark = pytest.mark.integration

DB_WRITE = str(Path(__file__).resolve().parent.parent / "db_write.py")
JOB_LOCK = str(Path(__file__).resolve().parent.parent / "job_lock.py")


def _run_db(args: list[str]) -> tuple[int, str]:
    result = subprocess.run(
        [sys.executable, DB_WRITE] + args,
        capture_output=True, text=True, timeout=15,
    )
    return result.returncode, result.stdout + result.stderr


def _run_lock(args: list[str]) -> tuple[int, str]:
    result = subprocess.run(
        [sys.executable, JOB_LOCK] + args,
        capture_output=True, text=True, timeout=15,
    )
    return result.returncode, result.stdout + result.stderr


@pytest.fixture
def smoke_branch(db_conn):
    """테스트용 더미 branch를 생성하고 테스트 후 삭제한다."""
    branch = f"test-smoke-{uuid.uuid4().hex[:8]}"
    now = datetime.now(timezone.utc)
    cur = db_conn.cursor()
    cur.execute(
        """
        INSERT INTO sdlc.ai_jobs
            (branch, sprint, task, status, owner, created_at, updated_at)
        VALUES (%s, 9999, 'smoke-test', 'analyzing', 'pytest', %s, %s)
        """,
        (branch, now, now),
    )
    db_conn.commit()
    cur.close()
    yield branch
    cur2 = db_conn.cursor()
    cur2.execute("DELETE FROM sdlc.ai_jobs WHERE branch = %s", (branch,))
    db_conn.commit()
    cur2.close()


def test_job_lifecycle(smoke_branch):
    """upsert-job(analyzing) → insert-step × 3 → get-gates 순서 검증."""
    b = smoke_branch

    # status → coding
    code, out = _run_db(["--action", "upsert-job", "--branch", b, "--status", "coding"])
    assert code == 0, f"upsert coding 실패: {out}"

    # impact step
    code, out = _run_db([
        "--action", "insert-step", "--branch", b,
        "--step-name", "impact", "--step-status", "done", "--step-summary", "LOW risk",
    ])
    assert code == 0, f"insert impact 실패: {out}"

    # test_gen step (test_generated → true)
    code, out = _run_db([
        "--action", "upsert-job", "--branch", b, "--test-generated",
    ])
    assert code == 0, f"upsert test-generated 실패: {out}"

    code, out = _run_db([
        "--action", "insert-step", "--branch", b,
        "--step-name", "test_gen", "--step-status", "done", "--step-summary", "불필요",
    ])
    assert code == 0

    # review step (review_completed → true)
    code, out = _run_db([
        "--action", "insert-step", "--branch", b,
        "--step-name", "review", "--step-status", "done", "--step-summary", "승인",
    ])
    assert code == 0, f"insert review 실패: {out}"

    # get-gates 검증
    code, out = _run_db(["--action", "get-gates", "--branch", b])
    assert code == 0, f"get-gates 실패: {out}"
    assert "test_generated=true" in out, f"test_generated gate 미충족: {out}"
    assert "review_completed=true" in out, f"review gate 미충족: {out}"
    assert "impact_done=true" in out, f"impact gate 미충족: {out}"


def test_lock_and_pipeline_integration(smoke_branch):
    """claim → step 기록 → release → list-active(0) 확인."""
    b = smoke_branch

    code, out = _run_lock(["claim", "--branch", b, "--owner", "pytest-smoke", "--ttl", "5"])
    assert code == 0, f"lock claim 실패: {out}"
    token = next(l.split("=", 1)[1] for l in out.splitlines() if l.startswith("LOCK_TOKEN="))

    code, out = _run_db([
        "--action", "insert-step", "--branch", b,
        "--step-name", "done", "--step-status", "done", "--step-summary", "smoke test",
    ])
    assert code == 0

    code, out = _run_lock(["release", "--branch", b, "--token", token])
    assert code == 0, f"lock release 실패: {out}"

    code, out = _run_lock(["status", "--branch", b])
    assert code == 0
    assert "unlocked" in out, f"release 후 status가 unlocked가 아님: {out}"


def test_gate_enforcement_before_completion(smoke_branch):
    """단계 미완료 상태에서 get-gates가 false를 반환하는지 확인."""
    b = smoke_branch

    code, out = _run_db(["--action", "get-gates", "--branch", b])
    assert code == 0

    assert "test_generated=false" in out, f"초기 test_generated는 false여야 함: {out}"
    assert "review_completed=false" in out, f"초기 review_completed는 false여야 함: {out}"
    assert "impact_done=false" in out, f"초기 impact_done은 false여야 함: {out}"
    assert "requirement_done=false" in out, f"초기 requirement_done은 false여야 함: {out}"
