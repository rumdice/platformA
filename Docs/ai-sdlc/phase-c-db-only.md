# AI_SDLC Phase C: DB 단독 운영

작성일: 2026-06-10 | 관련 스프린트: #52(AdoptPhaseCDbOnly)
**Phase C 선언일: 2026-06-10**

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
| `check_sdlc_consistency.py --strict` exit 0 | ✅ | gate 불일치 0건 |
| gate 검사 파일 fallback 제거 dry-run | ✅ | get-gates 5개 필드 반환 확인 |
| Sprint 1회 DB 단독 순환 확인 | ✅ | Sprint #52 완료 |

---

## 전환 절차 (완료)

### C.0 — 사전 dry-run (완료)

```bash
python .github/scripts/db_write.py \
  --action get-gates \
  --branch "$(git branch --show-current)"
```

5개 필드 (test_generated, review_completed, impact_done, adr_required, requirement_done) 반환 확인.

---

### C.1 — task JSON 신규 쓰기 중단 ✅

`/plan` SKILL.md에서 task JSON 생성 코드 제거, DB INSERT만 수행.
`AI/tasks/` 디렉토리: 기존 파일 유지, 신규 생성 없음.

---

### C.2 — cost-log.md 직접 append 중단 ✅

`/pr` SKILL.md에서 `AI/cost-log.md` Edit 도구 append 제거.
`generate_cost_log_from_db.py --output AI/reports/generated-cost-log-from-db.md` 자동 실행.

---

### C.3 — gate 검사 파일 fallback 제거 ✅

`/pr`과 `/done` SKILL.md에서 파일 fallback 제거.
DB 응답이 없으면 오류 처리.

> **주의**: 이 단계부터 PostgreSQL 연결이 SDLC 워크플로의 필수 조건이 된다.

---

### C.4 — check_sdlc_consistency.py 검사 범위 확장 ✅

`files_archived` 분류 추가 — DB status='done' 항목은 Phase C 정상 상태로 처리.

---

## 롤백 계획

Phase C 전환 후 문제가 발생하면:

1. task JSON 신규 쓰기 재활성화 (`/plan` SKILL.md 되돌리기)
2. cost-log.md append 재활성화 (`/pr` SKILL.md 되돌리기)
3. gate 검사 파일 fallback 복원 (`/pr`, `/done` SKILL.md 되돌리기)

기존 task JSON 파일은 삭제하지 않았으므로 즉시 복원 가능.

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

- [Phase A/B/C 로드맵](phases.md)
- [Auto Fix 정책](auto-fix.md)
- [DB Schema](db-schema.md)
