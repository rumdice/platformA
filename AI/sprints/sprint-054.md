# Sprint #54 — FixGatesStepBased

**기간**: 2026-06-10 ~  
**목표**: Phase C 게이트 판정 방식을 step 기반으로 완전 통일

## 태스크

- [x] `db_write.py` — `get-gates`의 `test_generated` 판정을 boolean OR step('test_gen') 기반으로 수정
- [x] `db_write.py` — `get-gates`의 `review_completed` 판정을 boolean OR step('review') 기반으로 수정

## 배경

Sprint #53에서 impact_done/requirement_done의 step 기반 판정 수정 후 재체크에서 추가 갭 발견:
- `/test-gen` skill은 `insert-step 'test_gen'`만 수행 — `ai_jobs.test_generated` boolean 미갱신
- `/review` skill은 `insert-step 'review'`만 수행 — `ai_jobs.review_completed` boolean 미갱신
- `get-gates`가 boolean 컬럼 직접 참조 → C# 코드 변경 포함 Phase C 스프린트에서 항상 false

OR 조건으로 구현하여 boolean이 set된 기존 스프린트와 step만 있는 신규 Phase C 스프린트 모두 동작.

## 참조

- Sprint #53 (`FixDoneSkillPhaseC`) — 동일 패턴 선행 수정 (impact_done, requirement_done)
