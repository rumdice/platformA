# Sprint #53 — FixDoneSkillPhaseC

**기간**: 2026-06-10 ~  
**목표**: Phase C 완성도 체크 후 발견된 게이트 판정 갭 수정

## 태스크

- [x] `db_write.py` — `get-gates`의 `impact_done` 판정을 `ai_jobs.impact IS NOT NULL` → step 기반으로 수정
- [x] `/requirement` SKILL.md — 7단계에 `insert-step requirement` DB 호출 추가 (Phase C requirement_done 게이트 충족)
- [x] `/done` SKILL.md — 헬퍼 섹션에 Phase C 예외 처리 추가 (task JSON 없음 → DB job 확인)
- [x] Sprint #52 DB gates backfill (`requirement` step 삽입)

## 배경

PR #81(AdoptPhaseCDbOnly) 머지 후 Phase C 완성도 점검에서 발견된 갭:
- `/impact` skill이 `ai_job_steps`에 'impact' step을 삽입하지만, `get-gates`는 `ai_jobs.impact (jsonb) IS NOT NULL`을 참조 → 항상 false
- `/requirement` skill이 DB를 전혀 호출하지 않아 `requirement_done`이 항상 false
- 두 게이트가 영구 false이면 Phase C에서 C# 코드 변경 스프린트가 `/done`에서 차단됨

## 참조

- `Docs/operations/ai-sdlc-phase-c-db-only-plan.md`
