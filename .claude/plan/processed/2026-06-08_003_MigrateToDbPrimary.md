# 요구사항 명세: MigrateToDbPrimary

작성일: 2026-06-08
브랜치: 2026-06-08_MigrateToDbPrimary
소스: task JSON summary + /workflow 인수

## 요구사항 요약

`db_write.py`의 `sprint_number` 컬럼명 버그를 수정하고, Sprint #44~#49 DB backfill 및 기존 불일치를 해소하여 PostgreSQL을 SDLC 상태의 primary 데이터 소스(Phase B)로 전환한다. 게이트 검사도 DB SELECT primary로 전환하고 `check_sdlc_consistency.py --strict` 통과를 확인한다.

## 상세 요구사항

1. **`db_write.py` 컬럼명 버그 수정**  
   - `action_upsert_job()` 내 `sprint_number` → `sprint` (실제 DB 컬럼명)  
   - 수정 후 `upsert-job` 호출이 성공하는지 검증

2. **`/plan` SKILL.md sprint 카운터 재수정**  
   - 현재: `ls AI/tasks/sprint*.json | wc -l` (파일 수 기반 → sprint30이 2개이므로 31 반환)  
   - 수정: `ls AI/tasks/sprint*.json | grep -o 'sprint[0-9]*' | grep -o '[0-9]*' | sort -n | tail -1` (최대 번호 기반)

3. **Sprint #44~#49 DB backfill**  
   - `migrate_tasks_to_postgres.py --apply` 재실행  
   - 6개 브랜치가 `sdlc.ai_jobs`에 INSERT되는지 확인

4. **DB 상태 불일치 수정**  
   - `2026-06-05_StabilizeSdlcPhase3DataFlow`: DB=coding → JSON=done 으로 DB UPDATE  
   - `test_generated`, `review_completed` gate 동기화

5. **`/pr` 스킬 게이트 검사 DB SELECT primary 전환**  
   - `db_write.py --action get-gates` 응답이 있으면 DB 값 우선 사용  
   - DB에 해당 브랜치가 없거나 응답 없으면 기존 파일 기반 fallback 유지  
   - 현재 SKILL.md의 "검사 0" 섹션이 이미 DB 조회 로직을 갖고 있으나 실제로 DB 값이 검사 3~7에 전달되지 않음 → 연결 수정

6. **model_run backfill**  
   - `sdlc.ai_model_runs`에 rows가 0건  
   - `insert_model_run.py`를 완료된 브랜치에 일괄 적용하는 `backfill_model_runs.py` 스크립트 신규 작성  
   - 완료된 task JSON (status=done, consume_tokens != null)을 대상으로 실행

7. **`check_sdlc_consistency.py --strict` 통과 확인**  
   - 위 1~6 완료 후 실행하여 Result: OK 확인  
   - Phase B 선언: `Docs/operations/ai-sdlc-db-migration-roadmap.md`에 Phase B 시작 날짜 기록

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|------|---------|
| `.github/scripts/db_write.py` | 수정 (1줄) |
| `.github/scripts/migrate_tasks_to_postgres.py` | 실행 (코드 수정 없음) |
| `.github/scripts/backfill_model_runs.py` | 신규 |
| `.claude/skills/plan/SKILL.md` | 수정 (sprint 카운터) |
| `.claude/skills/pr/SKILL.md` | 수정 (gate 검사 DB primary 연결) |
| `Docs/operations/ai-sdlc-db-migration-roadmap.md` | 수정 (Phase B 시작 날짜) |

## 제약 및 주의사항

- ADR-009: PostgreSQL SDLC DB — 기결정 사항, 충돌 없음
- `db_write.py` 수정 후 기존 `|| true` 동작 유지 필수 — C# 게임 서비스에 영향 없음
- `backfill_model_runs.py`: PostgreSQL 미실행 시 graceful skip (exit 0) 필수
- 파일 기반 fallback은 Phase C까지 유지 — 지금 삭제하지 않음
- DB UPDATE (`StabilizeSdlcPhase3DataFlow`)는 Python 스크립트로 실행 — 직접 SQL psql 실행 금지

## 구현 접근 방향

```
1. db_write.py 1줄 수정 → 즉시 검증 (python db_write.py --action upsert-job --branch test ...)
2. SKILL.md sprint 카운터 max 기반으로 수정
3. migrate_tasks_to_postgres.py --apply 실행 → DB row 수 확인
4. Python snippet으로 StabilizeSdlcPhase3DataFlow DB 상태 수정
5. /pr SKILL.md 게이트 검사 DB 값 연결 수정
6. backfill_model_runs.py 신규 작성 및 실행
7. check_sdlc_consistency.py --strict 실행 → Result: OK 확인
8. roadmap.md Phase B 시작 날짜 기록
```

## 검증 기준

- `python .github/scripts/db_write.py --action upsert-job --branch test-branch --sprint 99 --task Test --status analyzing` → `[db_write] upsert-job OK` 출력
- `python .github/scripts/check_sdlc_consistency.py --check --strict` → `Result: OK`
- `sdlc.ai_jobs` row 수 ≥ 30 (sprint #1~#50 중 task JSON이 있는 것)
- `sdlc.ai_model_runs` row 수 > 0
- `dotnet build PlatformA.sln` 성공 (C# 코드 변경 없음)
- `dotnet test PlatformA.sln` 133개 통과
