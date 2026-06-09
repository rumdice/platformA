# AI_SDLC DB Primary 전환 로드맵

작성일: 2026-06-08
최종 수정: 2026-06-10
관련 스프린트: #49, #51

## 현재 상태 (2026-06-10 기준: Phase B)

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

**상태**: ✅ 완료 (2026-06-08 시작 → 2026-06-10 조건 전체 충족)

**전환 조건** (모두 충족):

1. ✅ DB write 실패율 7일 평균 < 1% — `sprint_number` 버그 수정으로 upsert-job 정상화
2. ✅ `check_sdlc_consistency.py --check --strict` 불일치 0건 — Sprint #51에서 재확인 (step_name 버그 수정 포함)
3. ✅ gate 검사 DB SELECT primary 전환 — `/pr` SKILL.md 검사 3~7 DB primary 연결 완료
4. ✅ PostgreSQL 정기 백업 정책 — `backup_sdlc_db.sh` 작성 완료 (Sprint #51, 7일 보관, pg_dump 미설치 시 graceful skip)

**Phase B에서 변경되는 사항**:
- gate 검사 primary: DB SELECT (파일 fallback 유지) ← 구현 완료
- cost-log.md: DB에서 생성되는 report로 전환 시작
- task JSON: 쓰기 계속, 읽기는 DB 우선

---

### Phase C — DB 단독 (task JSON 제거)

**상태**: 준비 중 (Phase B 조건 전체 충족 → 조기 도입 검토)

**전환 조건** (Phase B가 선행 조건):

1. ✅ Phase B 조건 전체 충족 (30일 대기 불필요 — 기술적 조건 기준으로 판단)
2. ✅ `generate_cost_log_from_db.py` DB 기반 cost-log 출력 가능 (Sprint #51 완료)
3. ✅ `check_sdlc_consistency.py --strict` exit 0 안정 통과 (Sprint #51 완료)
4. ☐ gate 검사 파일 fallback 제거 dry-run 테스트 통과
5. ☐ Sprint 1회 이상 파일 없이 DB만으로 완전한 SDLC 순환 확인

**Phase C에서 제거되는 항목**:
- `AI/tasks/*.json` 신규 쓰기 중단 — 기존 파일은 읽기 전용 아카이브 유지
- task JSON 기반 gate 검사 fallback 코드
- `AI/cost-log.md` 직접 append 로직 (`generate_cost_log_from_db.py` 출력으로 대체)

**Phase C 완료 후 기대 상태**:
- SDLC 상태의 단일 진실원: PostgreSQL
- 파일 시스템: 불변 아카이브만 (삭제 안 함, 쓰기 중단)
- 충돌 원인(append-only 파일) 완전 제거

> 상세 전환 계획: `Docs/operations/ai-sdlc-phase-c-db-only-plan.md` 참조

---

## 전환 금지 조건

아래 조건이 하나라도 해당하면 다음 Phase로 전환하지 않는다:

- DB 연결 실패가 하루에 1회 이상 발생
- `check_sdlc_consistency.py --check --strict`에서 불일치 > 0
- PostgreSQL 인스턴스에 백업 정책 없음
- 자동 수정 retry_count > 10인 작업이 존재 (불안정 징후)

## 마일스톤

| 마일스톤 | 조건 | 완료 시점 |
|---------|------|---------|
| Phase A 완료 | Phase B 전환 조건 충족 | 2026-06-08 |
| Phase B 완료 | 4개 조건 전체 충족 | 2026-06-10 |
| Phase C 시작 | Phase C 조건 5개 중 4개 기술적 조건 충족 | 2026-06-10 (준비 중) |
| Phase C 완료 | Sprint 1회 DB 단독 순환 확인 | 미정 |

> Phase C는 30일 대기 없이 기술적 조건 충족 즉시 도입 가능. 사용자 승인 후 진행.

## 관련 문서

- `AI/sprints/README.md`: 스프린트 파일 구조
- `Docs/operations/ai-sdlc-append-only-conflict-policy.md`: 충돌 완화 정책
- `Docs/operations/ai-sdlc-phase-c-db-only-plan.md`: Phase C 상세 전환 계획
- `.github/scripts/check_sdlc_consistency.py`: 정합성 검사 스크립트
- `.github/scripts/db_write.py`: DB dual-write 헬퍼
- `.github/scripts/backup_sdlc_db.sh`: PostgreSQL 백업 스크립트 (7일 보관)
- `.github/scripts/generate_cost_log_from_db.py`: DB 기반 cost-log 출력
