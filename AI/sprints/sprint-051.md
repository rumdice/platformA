# Sprint #51 — PrepareDbPrimaryPhaseC

**기간**: 2026-06-10 ~  
**목표**: Phase B 차단 버그 수정 및 Phase C(DB 단독) 전환 기반 구축

## 태스크

- [ ] `db_write.py` — `name` → `step_name` 컬럼명 버그 수정 (insert-step, get-gates)
- [ ] Sprint #50 gate DB 재동기화 (test_generated, review_completed 재반영)
- [ ] `check_sdlc_consistency.py` — model_run_missing LEGACY exception 처리 추가
- [ ] `check_sdlc_consistency.py --strict` 통과 복원
- [ ] PostgreSQL 백업 정책 수립 (`backup_sdlc_db.sh` + Phase B 조건 4번 완료 처리)
- [ ] `generate_cost_log_from_db.py` 신규 작성 (DB → markdown cost-log export)
- [ ] Phase C 전환 조건 문서화 (`ai-sdlc-db-migration-roadmap.md` Phase C 조건 갱신)

## 배경

Sprint #50 완료 후 `check_sdlc_consistency.py --strict`가 FAIL 상태로 재전환됨:
- `ai_job_steps.name` 컬럼이 실제로는 `step_name`이어서 insert-step/get-gates 전부 실패
- 이로 인해 Sprint #50 gate(test_generated, review_completed)가 DB에 미반영
- model_run_missing 16건은 전부 cost 추적 이전 레거시 → LEGACY exception 처리 필요

## 참조

- `Docs/operations/ai-sdlc-db-migration-roadmap.md` — Phase B 조건 4번(백업 정책) 미충족
- `.github/scripts/db_write.py` — 수정 대상
- `.github/scripts/check_sdlc_consistency.py` — LEGACY 처리 추가 대상
