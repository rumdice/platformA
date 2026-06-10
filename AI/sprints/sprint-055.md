---
sprint: 55
title: Phase C 경화 — owner·localhost 차단·보고서 gitignore
branch: 2026-06-10_PhaseCharden
date: 2026-06-10
status: done
---

# Sprint #55 — Phase C Hardening

## 목표

Phase C(DB 단독 상태 관리) 경화 작업:
1. `ai_jobs.owner` 컬럼 추가 — 1인 다수 agent / 팀 동시 개발 시 작업 소유자 추적
2. SDLC_TEAM_MODE 환경변수 기반 localhost 차단 — 팀 환경에서 로컬 DB 사용 방지
3. `AI/reports/generated-*.md` gitignore 등록 — DB 재생성 가능 파일의 git 충돌 방지
4. 게이트 step 기반 판정 통일 — 이전 PR(#82)의 보완 (review/test_gen OR 조건 안정화)

## 완료 태스크

- [x] `sdlc_db_migrations.sql` Migration 003 — `ai_jobs.owner VARCHAR(100)` 컬럼 추가
- [x] `db_write.py` — `_get_git_owner()` 추가, upsert 시 owner 자동 감지 및 COALESCE 보존
- [x] `db_write.py` — `_LOCALHOST_ALIASES`, `_check_team_mode_localhost()`, `SDLC_TEAM_MODE=1` 차단
- [x] `db_write.py` — `list-active` 출력에 owner 포함
- [x] `check_sdlc_consistency.py` — owner 컬럼 조회 및 stuck_sprints 출력에 owner 포함
- [x] `.gitignore` — `AI/reports/generated-*.md` 섹션 추가
- [x] `AI/reports/generated-cost-log-from-db.md` git untrack (git rm --cached)
- [x] 기존 35개 ai_jobs 행 owner backfill (rumdice)

## 참조

- 관련 PR: #84
- 선행 작업: Sprint #53 (PhaseC 게이트 갭 수정), Sprint #54 (step 기반 판정 통일)
