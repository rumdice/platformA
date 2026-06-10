---
sprint: 57
title: Phase C Job Lock — DB 기반 동시 실행 제어
branch: 2026-06-11_CompletePhaseCJobLock
date: 2026-06-11
status: done
completed: 2026-06-11
pr: https://github.com/rumdice/platformA/pull/86
---

# Sprint #57 — Phase C Job Lock

## 목표

ai_jobs 테이블에 DB 기반 Job Lock을 추가하여 동일 job의 다중 agent 동시 실행을 방지한다.

## 태스크

- [x] `sdlc_db_migrations.sql` — Migration 004: lock 컬럼 6개 + 인덱스 추가
- [x] `job_lock.py` 신규 작성 — claim/release/heartbeat/status/expire/list-active
- [x] `db_write.py` — list-active에 lock 상태 컬럼 추가
- [x] `check_sdlc_consistency.py` — stale_locks/invalid_locks 검사 추가
- [x] `.claude/skills/start/SKILL.md` — 1단계 직후 lock claim 추가
- [x] `.claude/skills/pr/SKILL.md` — 5단계 직전 lock release 추가
- [x] `.claude/skills/done/SKILL.md` — 4.5단계 heartbeat 추가
- [x] `.claude/skills/workflow/SKILL.md` — 각 단계 전환 시 heartbeat 추가
- [x] `.claude/hooks/session-start.sh` — Active/Stale lock 섹션 추가
- [x] `Docs/operations/ai-sdlc-job-lock-policy.md` 신규 작성
- [x] `Docs/operations/ai-sdlc-auto-fix-policy.md` — n8n lock 정책 추가
- [x] `.gitignore` — `.ai_sdlc_lock` 추가

## 배경

Phase C(DB 단독 운영) 전환 후 파일 충돌은 해소됐으나, 동일 job을 두 agent가 동시에 처리하는
문제가 남아 있다. n8n auto-fix + 사용자 /workflow 동시 실행 시 DB 상태가 역행하거나 step 기록이
충돌할 수 있다. PostgreSQL row-level atomic UPDATE로 "하나의 job에 하나의 agent만" 원칙을 강제한다.

## 참조

- DB job: `sdlc.ai_jobs.branch = 2026-06-11_CompletePhaseCJobLock`
- 계획 파일: `C:\Users\rumdi\.claude\plans\hazy-booping-moore.md`
- 선행 스프린트: #52(Phase C 전환), #55(Phase C 경화)
