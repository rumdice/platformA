# Sprint #50 — MigrateToDbPrimary

**기간**: 2026-06-08 ~  
**목표**: db_write.py 버그 수정 및 DB backfill로 PostgreSQL SDLC Phase B primary 전환

## 태스크

- [ ] `db_write.py` — `sprint_number` → `sprint` 컬럼명 버그 수정 (1줄)
- [ ] `.claude/skills/plan/SKILL.md` — sprint 카운터 `max(sprint number)+1` 방식으로 재수정
- [ ] `migrate_tasks_to_postgres.py --apply` 재실행 — Sprint #44~#49 DB backfill (6개 누락)
- [ ] DB 상태 불일치 수정 — `StabilizeSdlcPhase3DataFlow` status/gate 동기화
- [ ] `/pr 스킬` 게이트 검사 DB SELECT primary 전환 (파일 fallback 유지)
- [ ] model_run backfill — 완료된 22개 브랜치 `insert_model_run.py` 일괄 적용
- [ ] `check_sdlc_consistency.py --strict` 통과 확인 → Phase B 선언

## 배경

Sprint #49에서 dual-write 체계 구축 완료 후, Phase B 전환을 위한 블로커 제거:
1. `sprint_number` 컬럼명 버그로 Sprint #44~#49 DB write 전부 실패
2. consistency check에서 6개 브랜치 누락 + 상태 불일치 검출
3. /plan sprint 카운터가 파일 수 기반으로 잘못 계산 (max 번호 기반으로 수정 필요)

## 참조

- `Docs/operations/ai-sdlc-db-migration-roadmap.md` — Phase B 전환 조건
- `.github/scripts/check_sdlc_consistency.py` — 정합성 검사
- `.github/scripts/db_write.py` — dual-write 헬퍼
