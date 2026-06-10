---
sprint: 58
title: AI_SDLC 워크플로우 테스트 보강
branch: 2026-06-11_AddBranchConflictGuard
date: 2026-06-11
status: done
completed: 2026-06-11
---

# Sprint #58 — AI_SDLC 워크플로우 테스트 보강

## 목표

AI_SDLC 파이프라인의 정책-구현 불일치 해소 및 자동화 테스트 도입으로 파이프라인 신뢰성을 확보한다.

## 태스크

- [ ] `auto-fix.yml` — Job Lock claim/release 스텝 추가 (정책 구현 완성)
- [ ] `.claude/skills/qa-failure/SKILL.md` — 0단계 lock claim + 마지막 단계 lock release 추가
- [ ] `.github/scripts/tests/test_job_lock.py` — pytest 8개 시나리오 자동화
- [ ] `.github/scripts/tests/test_sdlc_pipeline.py` — E2E smoke test 3개 시나리오
- [ ] `.github/scripts/tests/test_migration_idempotency.py` — Migration 멱등성 검증
- [ ] `sdlc_db_migrations.sql` — Migration Down(rollback) SQL 블록 추가
- [ ] `.github/workflows/sdlc-python-test.yml` — pytest 자동 실행 워크플로우 신규

## 배경

Sprint #57(Job Lock 구현) 이후 `ai-sdlc-auto-fix-policy.md` Section 8에
"n8n auto-fix는 lock claim 필수"라고 명시했지만, 실제 `auto-fix.yml`과 `/qa-failure`
스킬에는 job_lock.py 호출이 없는 불일치가 발견됨.
또한 job_lock.py, DB 파이프라인, Migration에 대한 자동화 테스트가 전무하여 회귀 감지 불가.

## 참조

- DB job: `sdlc.ai_jobs.branch = 2026-06-11_AddBranchConflictGuard`
- 선행 스프린트: #57(Job Lock), #55(Phase C 경화)
- 관련 정책: `Docs/operations/ai-sdlc-auto-fix-policy.md` Section 8
