# AI_SDLC Phase A/B/C — DB Primary 전환 로드맵

작성일: 2026-06-08 | 최종 수정: 2026-06-11

## 현재 상태: Phase C — DB 단독 운영 (2026-06-10 ~)

| 데이터 소스 | 역할 | 신뢰도 |
|------------|------|--------|
| PostgreSQL `sdlc.ai_jobs` | **단일 진실 공급원** | primary (필수) |
| `AI/tasks/*.json` | 읽기 전용 아카이브 | 쓰기 중단 |
| `AI/SPRINT.md` | 자동 재생성 파일 | DB에서 생성 |
| `AI/cost-log.md` | 읽기 전용 아카이브 | DB report로 대체 |

## 단계별 전환 이력

### Phase A — dual-write (파일 우선)

**상태**: ✅ 완료 (2026-06-05 ~ 2026-06-08)

- 모든 SDLC 스킬이 파일 + DB 동시 기록
- DB 실패 시 파일 fallback (exit 0, `|| true`)
- `check_sdlc_consistency.py`로 정합성 감시

---

### Phase B — DB 우선 (파일 보조)

**상태**: ✅ 완료 (2026-06-08 ~ 2026-06-10)

- gate 검사 primary: DB SELECT (파일 fallback 유지)
- cost-log.md: DB에서 생성되는 report로 전환 시작

**Phase B 종료 조건** (4개 충족):
1. ✅ DB write 실패율 7일 평균 < 1%
2. ✅ `check_sdlc_consistency.py --check --strict` 불일치 0건
3. ✅ gate 검사 DB SELECT primary 전환
4. ✅ PostgreSQL 정기 백업 정책 (`backup_sdlc_db.sh` 7일 보관)

---

### Phase C — DB 단독 (2026-06-10 ~)

**상태**: ✅ 운영 중

**제거된 항목**:
- `AI/tasks/*.json` 신규 쓰기
- task JSON 기반 gate 검사 fallback
- `AI/cost-log.md` 직접 append

**Phase C 전환 조건** (5개 충족):
1. ✅ Phase B 조건 4개 전체 충족
2. ✅ `generate_cost_log_from_db.py` DB 기반 cost-log 출력 가능
3. ✅ `check_sdlc_consistency.py --strict` exit 0 안정
4. ✅ gate 검사 파일 fallback 제거 dry-run 통과
5. ✅ Sprint #52(AdoptPhaseCDbOnly) — Phase C 첫 스프린트 완료

자세한 전환 절차: [Phase C DB 단독 운영](phase-c-db-only.md)

## Append-only 파일 충돌 완화 정책

Phase A 이전에는 `AI/SPRINT.md`와 `AI/cost-log.md`에 같은 날 여러 PR이 동시에 수정하면
merge conflict가 발생했습니다. Phase A에서 아래 정책으로 완화, Phase C에서 근본 해결되었습니다.

### SPRINT.md (Phase A → C 전환)

- **Phase A**: `AI/sprints/sprint-NNN.md`로 분산, SPRINT.md는 인덱스/요약만 유지
- **Phase C**: SPRINT.md는 PR 머지 후 `generate_sprint_md.py`가 DB 기반으로 자동 재생성

### cost-log.md (Phase A → C 전환)

- **Phase A**: `sdlc.ai_model_runs`에 병행 기록, 충돌 시 스프린트 번호 오름차순 정렬
- **Phase C**: `AI/cost-log.md` append 중단, `generate_cost_log_from_db.py`로 DB→파일 변환

### 충돌 발생 시 수동 해결 (Phase B까지 유효)

SPRINT.md 충돌: 두 섹션 모두 보존, 낮은 번호 먼저 배치.
cost-log.md 충돌: 두 행 모두 보존, 스프린트 번호 오름차순 정렬.

## 전환 금지 조건

아래 조건이 하나라도 해당하면 다음 Phase로 전환하지 않는다:
- DB 연결 실패가 하루에 1회 이상 발생
- `check_sdlc_consistency.py --strict`에서 불일치 > 0
- PostgreSQL 인스턴스에 백업 정책 없음

## 마일스톤

| 마일스톤 | 완료 시점 |
|---------|---------|
| Phase A 완료 | 2026-06-08 |
| Phase B 완료 | 2026-06-10 |
| Phase C 선언 | 2026-06-10 (Sprint #52) |
