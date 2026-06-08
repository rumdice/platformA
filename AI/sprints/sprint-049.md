# Sprint #49 — AI_SDLC Phase 3 운영 안정화

작성일: 2026-06-08
브랜치: `2026-06-08_StabilizeSdlcPhase3Ops`
규모: L
위험도: LOW (SDLC 툴링/문서만 변경, C# 코드 미변경)

## 목표

2026-06-05에 급격히 확장된 AI_SDLC Phase 3를 안정화한다.
append-only 충돌 완화, DB/JSON 정합성 검사, DB 실패 가시화, 자동 수정 safety policy 정리.

## 작업 목록

### 진행 중

- [ ] `AI/sprints/` 구조 도입 — 스프린트별 개별 파일 관리 (append-only 충돌 완화)
- [ ] `AI/SPRINT.md` 인덱스/요약 역할로 점진 전환
- [ ] `check_sdlc_consistency.py` — PostgreSQL ↔ task JSON 정합성 검사
- [ ] `db_write.py` 실패 로그 기록 추가 (`AI/logs/db-write-failures/`)
- [ ] `.claude/hooks/session-start.sh` 업데이트 — DB write 실패 표시 + AI/sprints/ 파일 읽기
- [ ] `.claude/skills/plan/SKILL.md` sprint 카운터 수정 (task JSON 파일 수 기반)
- [ ] `Docs/operations/ai-sdlc-auto-fix-policy.md` 작성
- [ ] `Docs/operations/ai-sdlc-append-only-conflict-policy.md` 작성
- [ ] `Docs/operations/ai-sdlc-db-migration-roadmap.md` 작성 (dual-write→DB-only 전환 기준)

## 배경

2026-06-05 workreport에서 SPRINT.md / cost-log.md append-only 충돌 패턴이 확인됨.
동일 날짜 다수 브랜치 작업 시 merge conflict 반복 발생. 추가로:

- db_write.py의 `|| true` 구조로 DB 기록 실패가 숨겨질 수 있음
- dual-write(파일+DB) 전환 기준이 명시되지 않음
- 자동 수정 허용 범위가 문서화되지 않음

## 참조

- PR #72-#77 (2026-06-05): Phase 3 기반 구축
- `AI/workreport/2026-06-05.md`: 충돌 패턴 최초 발견
- `.github/scripts/db_write.py`: dual-write 헬퍼
- `Docs/operations/`: 이번 스프린트에서 신규 정책 문서 3종 추가
