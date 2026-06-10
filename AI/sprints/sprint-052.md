# Sprint #52 — AdoptPhaseCDbOnly

**기간**: 2026-06-10 ~  
**목표**: AI_SDLC Phase C 조기 도입 — PostgreSQL을 단일 진실원으로 전환, 파일 기반 쓰기 완전 중단

## 태스크

- [x] `generate_cost_log_from_db.py` — 스키마 수정 (model_id → join 기반, ai_jobs 집계값 사용)
- [x] `insert_model_run.py` — em dash cp949 수정 (→ hyphen)
- [x] `/plan` SKILL.md — C.1: task JSON 생성 코드 제거, DB upsert-job만 수행
- [x] `/pr` SKILL.md — C.2: cost-log.md append 제거, generate_cost_log_from_db.py 자동 실행
- [x] `/done`, `/pr` SKILL.md — C.3: gate 검사 파일 fallback 제거 (DB 필수화)
- [x] `check_sdlc_consistency.py` — Phase C 상태(JSON 없는 브랜치) 정상 처리
- [x] `ai-sdlc-phase-c-db-only-plan.md` — 조건 5 완료 처리 및 Phase C 선언

## 배경

Phase B 조건 4개 전체 충족 (Sprint #51 완료). Phase C 기술 조건도 4/5 충족.
30일 대기 대신 기술 조건 기준으로 조기 도입 결정.

주요 변경 원칙:
- 파일 삭제 없음 — 기존 JSON/cost-log.md는 읽기 전용 아카이브로 유지
- DB 연결 실패 시 graceful skip(exit 0) 제거 → 명시적 오류 처리로 전환
- `/plan` 이후 task JSON 생성 없음, 대신 ai_jobs INSERT만 수행

## 참조

- `Docs/operations/ai-sdlc-phase-c-db-only-plan.md` — 상세 전환 절차
- `Docs/operations/ai-sdlc-db-migration-roadmap.md` — Phase C 조건 및 마일스톤
