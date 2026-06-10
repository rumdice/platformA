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
