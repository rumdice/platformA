# AI_SDLC DB Primary 전환 로드맵

작성일: 2026-06-08
관련 스프린트: #49

## 현재 상태 (2026-06-08 기준: Phase A)

| 데이터 소스 | 역할 | 신뢰도 |
|------------|------|--------|
| task JSON (`AI/tasks/*.json`) | primary/fallback | 항상 기록 |
| `AI/SPRINT.md` | 인덱스/요약 | 인덱스로 점진 전환 중 |
| `AI/cost-log.md` | 비용 기록 | 파일 primary, DB secondary |
| PostgreSQL `sdlc.ai_jobs` | secondary | dual-write, 실패 시 파일 fallback |
| PostgreSQL `sdlc.ai_job_steps` | secondary | dual-write |
| PostgreSQL `sdlc.ai_model_runs` | cost DB | /pr 완료 시 기록 |

## 장기 목표

파일 기반 SDLC를 완전히 DB primary로 전환한다.

## 단계별 전환 계획

### Phase A — dual-write (파일 우선)

**상태**: ✅ 완료 (2026-06-05 ~ 2026-06-08)

- 모든 SDLC 스킬이 파일 + DB 동시 기록
- DB 실패 시 파일 fallback (exit 0, `|| true`)
- DB write 실패는 `AI/logs/db-write-failures/YYYY-MM-DD.log`에 기록
- `check_sdlc_consistency.py`로 정합성 감시

**Phase A 종료 조건**: Phase B 전환 조건 모두 충족 시

---

### Phase B — DB 우선 (파일 보조)

**상태**: ✅ 진행 중 (2026-06-08 시작)

**전환 조건** (모두 충족):

1. ✅ DB write 실패율 7일 평균 < 1% — `sprint_number` 버그 수정으로 upsert-job 정상화
2. ✅ `check_sdlc_consistency.py --check --strict` 불일치 0건 — Sprint #50에서 확인
3. ✅ gate 검사 DB SELECT primary 전환 — `/pr` SKILL.md 검사 3~7 DB primary 연결 완료
4. ⚠️ PostgreSQL 정기 백업 정책 — 로컬 개발 환경, 백업 정책 미수립 (Phase B 진행 중 수립 예정)

**Phase B에서 변경되는 사항**:
- gate 검사 primary: DB SELECT (파일 fallback 유지) ← 구현 완료
- cost-log.md: DB에서 생성되는 report로 전환 시작
- task JSON: 쓰기 계속, 읽기는 DB 우선

---

### Phase C — DB 단독 (task JSON 제거)

**전환 조건** (Phase B가 선행 조건):

1. Phase B 안정 운영 30일 이상
2. task JSON 파일 역할을 DB가 완전히 대체 가능함을 검증
3. `AI/SPRINT.md`, `AI/cost-log.md`를 DB 생성 report로 완전 전환

**Phase C에서 제거되는 항목**:
- `AI/tasks/*.json` — DB migration history로 아카이브
- task JSON 기반 gate 검사 코드
- cost-log.md 직접 append 로직

**Phase C 완료 후 기대 상태**:
- SDLC 상태의 단일 진실원: PostgreSQL
- 파일 시스템: 불변 아카이브만 (삭제 안 함, 쓰기 중단)
- 충돌 원인(append-only 파일) 완전 제거

---

## 전환 금지 조건

아래 조건이 하나라도 해당하면 다음 Phase로 전환하지 않는다:

- DB 연결 실패가 하루에 1회 이상 발생
- `check_sdlc_consistency.py --check --strict`에서 불일치 > 0
- PostgreSQL 인스턴스에 백업 정책 없음
- 자동 수정 retry_count > 10인 작업이 존재 (불안정 징후)

## 마일스톤

| 마일스톤 | 조건 | 예상 시점 |
|---------|------|---------|
| Phase A 완료 | Phase B 전환 조건 충족 | 미정 |
| Phase B 시작 | Phase A 완료 | 미정 |
| Phase C 시작 | Phase B 안정 30일 | 미정 |

> 예상 시점은 운영 안정성에 따라 결정하며, 강제 일정 없음.

## 관련 문서

- `AI/sprints/README.md`: 스프린트 파일 구조
- `Docs/operations/ai-sdlc-append-only-conflict-policy.md`: 충돌 완화 정책
- `.github/scripts/check_sdlc_consistency.py`: 정합성 검사 스크립트
- `.github/scripts/db_write.py`: DB dual-write 헬퍼
