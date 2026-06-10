-- AI_SDLC PostgreSQL 스키마 마이그레이션 이력
-- 적용 순서: 번호 순서대로 실행한다.
-- 적용 방법: psql -h localhost -U platforma -d platforma_sdlc -f sdlc_db_migrations.sql

-- =============================================================================
-- Migration 001 — 초기 스키마 (Phase A)
-- 날짜: 2026-06-05
-- =============================================================================
-- sdlc 스키마, ai_jobs, ai_job_steps, ai_model_runs 테이블 초기 생성.
-- 상세 DDL은 PlatformA.SdlcDB.Lib 마이그레이션 파일 참조.

-- =============================================================================
-- Migration 002 — Phase C 동시성 안전 강화 (Phase C Hardening)
-- 날짜: 2026-06-10
-- 관련 PR: #81 (AdoptPhaseCDbOnly)
-- 적용 조건: sdlc.ai_jobs와 sdlc.ai_job_steps가 존재해야 함
-- =============================================================================

-- 2-1. sprint 번호 시퀀스 생성
--      목적: SELECT MAX(sprint)+1 TOCTOU 레이스 제거
--      시작값: 53 (Sprint #52까지 완료된 상태 기준)
--      주의: 이미 존재하면 무시 (IF NOT EXISTS)
CREATE SEQUENCE IF NOT EXISTS sdlc.sprint_seq
    START WITH 53
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

COMMENT ON SEQUENCE sdlc.sprint_seq IS
    'AI_SDLC 스프린트 번호 시퀀스. /plan 스킬이 nextval()로 충돌 없는 번호를 발급받음.';

-- 2-2. ai_job_steps(job_id, step_name) 유니크 인덱스
--      목적: 동일 job에 동일 step이 중복 삽입되는 것을 DB 레벨에서 방지
--      ON CONFLICT (job_id, step_name) DO UPDATE 패턴으로 멱등성 보장
CREATE UNIQUE INDEX IF NOT EXISTS ux_ai_job_steps_job_step
    ON sdlc.ai_job_steps(job_id, step_name);

COMMENT ON INDEX sdlc.ux_ai_job_steps_job_step IS
    '동일 job+step 중복 방지. db_write.py insert-step이 ON CONFLICT DO UPDATE로 멱등 삽입.';

-- =============================================================================
-- Migration 003 — ai_jobs.owner 컬럼 추가
-- 날짜: 2026-06-10
-- 관련 PR: #84 (PhaseCharden)
-- 목적: 1인 다수 agent / 팀 동시 개발 시 작업 소유자 추적
--       owner = git config user.name (db_write.py가 자동 감지하여 INSERT 시 설정)
-- =============================================================================

ALTER TABLE sdlc.ai_jobs
    ADD COLUMN IF NOT EXISTS owner VARCHAR(100);

COMMENT ON COLUMN sdlc.ai_jobs.owner IS
    '작업 소유자 (git config user.name). /plan 실행 시 자동 설정. 팀 환경에서 작업 귀속 추적에 사용.';

-- =============================================================================
-- Migration 004 — ai_jobs job lock 컬럼 추가
-- 날짜: 2026-06-11
-- 목적: 동일 branch에 대한 다중 agent 동시 실행을 DB 레벨에서 방지.
--       /start에서 claim, /pr에서 release, 단계 전환 시 heartbeat.
-- =============================================================================

ALTER TABLE sdlc.ai_jobs
    ADD COLUMN IF NOT EXISTS locked_by         TEXT         NULL,
    ADD COLUMN IF NOT EXISTS locked_at         TIMESTAMPTZ  NULL,
    ADD COLUMN IF NOT EXISTS lock_expires_at   TIMESTAMPTZ  NULL,
    ADD COLUMN IF NOT EXISTS lock_token        TEXT         NULL,
    ADD COLUMN IF NOT EXISTS agent_id          TEXT         NULL,
    ADD COLUMN IF NOT EXISTS last_heartbeat_at TIMESTAMPTZ  NULL;

COMMENT ON COLUMN sdlc.ai_jobs.locked_by IS
    '현재 lock 소유자 (git user.name 또는 agent 식별자). NULL이면 lock 없음.';
COMMENT ON COLUMN sdlc.ai_jobs.locked_at IS
    'lock 획득 시각.';
COMMENT ON COLUMN sdlc.ai_jobs.lock_expires_at IS
    'lock 만료 시각. 이 시각이 지나면 다른 agent가 lock을 획득할 수 있다. 기본 TTL: 60분.';
COMMENT ON COLUMN sdlc.ai_jobs.lock_token IS
    'release/heartbeat 검증용 UUID. claim 시 생성, release 시 token 일치 확인.';
COMMENT ON COLUMN sdlc.ai_jobs.agent_id IS
    '실행 주체 식별자 (claude-code, n8n, auto-fix 등).';
COMMENT ON COLUMN sdlc.ai_jobs.last_heartbeat_at IS
    '마지막 heartbeat 시각. long-running 작업의 생존 신호.';

CREATE INDEX IF NOT EXISTS ix_ai_jobs_lock_expires
    ON sdlc.ai_jobs(lock_expires_at) WHERE lock_expires_at IS NOT NULL;

COMMENT ON INDEX sdlc.ix_ai_jobs_lock_expires IS
    'lock 만료 검색 최적화. expire 명령과 stale lock 감지에 사용.';

-- =============================================================================
-- Migration 004 DOWN (rollback) — 아래 SQL은 주석 처리됨, 필요 시 수동 실행
-- 주의: 컬럼 삭제 전 locked_by IS NOT NULL 인 행이 없는지 확인할 것
-- =============================================================================
-- DROP INDEX IF EXISTS sdlc.ix_ai_jobs_lock_expires;
-- ALTER TABLE sdlc.ai_jobs
--     DROP COLUMN IF EXISTS last_heartbeat_at,
--     DROP COLUMN IF EXISTS agent_id,
--     DROP COLUMN IF EXISTS lock_token,
--     DROP COLUMN IF EXISTS lock_expires_at,
--     DROP COLUMN IF EXISTS locked_at,
--     DROP COLUMN IF EXISTS locked_by;
