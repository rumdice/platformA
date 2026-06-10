# AI_SDLC Phase C: DB 단독 전환 계획

작성일: 2026-06-10
관련 스프린트: #51 (PrepareDbPrimaryPhaseC), #52 (AdoptPhaseCDbOnly)
선행 조건: Phase B 완료 (2026-06-10)
**Phase C 선언일: 2026-06-10** (Sprint #52 완료 기준)

---

## 개요

Phase C는 task JSON 파일 신규 쓰기를 중단하고 PostgreSQL을 SDLC 상태의 단일 진실원으로 삼는 단계다.
파일은 삭제하지 않고 읽기 전용 아카이브로 유지한다.

---

## 현재 상태 체크리스트 (2026-06-10)

| 조건 | 상태 | 비고 |
|------|------|------|
| Phase B 조건 4개 전체 충족 | ✅ | Sprint #51에서 확인 |
| `generate_cost_log_from_db.py` 작성 | ✅ | DB → markdown cost-log 출력 가능 |
| `check_sdlc_consistency.py --strict` exit 0 | ✅ | gate 불일치 0건, model_run_legacy 16건 (LEGACY exception) |
| gate 검사 파일 fallback 제거 dry-run | ✅ | get-gates 5개 필드 반환 확인 (Sprint #51) |
| Sprint 1회 DB 단독 순환 확인 | ✅ | Sprint #52 (AdoptPhaseCDbOnly) — Phase C 첫 스프린트 완료 |

---

## 전환 절차

### C.0 — 사전 dry-run

파일 fallback 없이 DB만 사용했을 때 gate 검사가 정상 동작하는지 확인한다.

```bash
# 현재 브랜치 게이트 값 DB에서 직접 조회
python .github/scripts/db_write.py \
  --action get-gates \
  --branch "$(git branch --show-current)"
```

5개 필드 (test_generated, review_completed, impact_done, adr_required, requirement_done) 모두 반환되면 통과.

---

### C.1 — task JSON 신규 쓰기 중단 ✅ (Sprint #52 완료)

**대상 파일**: `.claude/skills/plan/SKILL.md`

변경 내용:
- task JSON 생성 코드 제거
- DB INSERT만 수행 (upsert-job)
- `AI/tasks/` 디렉토리: 기존 파일 유지, 신규 생성 없음

이후 스프린트부터 `AI/tasks/*.json`이 새로 생성되지 않는다.

---

### C.2 — cost-log.md 직접 append 중단 ✅ (Sprint #52 완료)

**대상 파일**: `.claude/skills/pr/SKILL.md`

변경 내용:
- `AI/cost-log.md` Edit 도구 append 제거
- `/pr` 완료 후 `generate_cost_log_from_db.py --output AI/reports/generated-cost-log-from-db.md` 자동 실행
- `AI/cost-log.md`는 읽기 전용 아카이브로 유지

---

### C.3 — gate 검사 파일 fallback 제거 ✅ (Sprint #52 완료)

**대상 파일**: `.claude/skills/pr/SKILL.md`, `.claude/skills/done/SKILL.md`

변경 내용:
- DB 응답이 없으면 fallback 없이 오류 처리
- DB 연결 필수 (graceful skip 제거)

> **주의**: 이 단계부터 PostgreSQL 연결이 SDLC 워크플로의 필수 조건이 된다.
> 로컬 환경에서 DB가 없으면 `/pr`, `/done` 실행 불가.

---

### C.4 — check_sdlc_consistency.py 검사 범위 확장 ✅ (Sprint #52 완료)

Phase C 전환 후에는 `missing_in_files` (DB에는 있지만 JSON 없음) 카운트가 증가한다.
`files_archived` 분류 추가 — DB status='done' 항목은 Phase C 정상 상태로 처리하여 WARN/FAIL 대상 제외.

---

## 롤백 계획

Phase C 전환 후 문제가 발생하면:

1. task JSON 신규 쓰기 재활성화 (plan SKILL.md 되돌리기)
2. cost-log.md append 재활성화
3. gate 검사 파일 fallback 복원

기존 task JSON 파일은 삭제하지 않았으므로 즉시 복원 가능.

---

## 전환 금지 조건

아래 조건 중 하나라도 해당하면 Phase C로 전환하지 않는다:

- DB 연결 실패가 하루에 1회 이상 발생
- `check_sdlc_consistency.py --strict`에서 gate 불일치 > 0
- `backup_sdlc_db.sh` 실행 이력 없음 (백업 미실행)
- `get-gates` 호출 시 5개 필드 중 하나 이상 누락

---

## 관련 스크립트

| 스크립트 | 용도 |
|---------|------|
| `.github/scripts/db_write.py` | SDLC DB 쓰기/읽기 헬퍼 |
| `.github/scripts/check_sdlc_consistency.py` | JSON ↔ DB 정합성 검사 |
| `.github/scripts/backup_sdlc_db.sh` | PostgreSQL 백업 (7일 보관) |
| `.github/scripts/generate_cost_log_from_db.py` | DB 기반 cost-log markdown 생성 |

---

## 관련 문서

- `Docs/operations/ai-sdlc-db-migration-roadmap.md`: 전체 단계 로드맵
- `Docs/operations/ai-sdlc-auto-fix-policy.md`: 자동 수정 안전 정책
- `Docs/operations/ai-sdlc-append-only-conflict-policy.md`: 충돌 완화 정책
