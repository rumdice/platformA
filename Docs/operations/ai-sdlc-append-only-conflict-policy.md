# AI_SDLC Append-only 파일 충돌 완화 정책

작성일: 2026-06-08
관련 스프린트: #49

## 문제

`AI/SPRINT.md`, `AI/cost-log.md`는 여러 브랜치가 같은 날짜에 동시에 수정할 경우
merge conflict가 발생하기 쉽다.

2026-06-05 workreport에서 최초 확인:
- 당일 6개 PR (#72~#77)이 모두 SPRINT.md와 cost-log.md 마지막 줄을 수정
- GitHub에서 PR 머지 순서에 따라 다음 PR들이 충돌 해결 필요

## 정책

### SPRINT.md

**현재 (Phase A)**:
- `AI/SPRINT.md`는 인덱스/요약 파일로 축소한다.
- 새 스프린트 상세는 `AI/sprints/sprint-NNN.md`에 작성한다.
- SPRINT.md에는 Active Sprint 테이블만 업데이트한다 (테이블 행 1개 추가/수정 — 충돌 최소화).
- 기존 스프린트 #1~#48 내역은 SPRINT.md 하단에 보존한다.

**장기 (Phase B/C)**:
- SPRINT.md를 완전 인덱스 테이블로 전환하여 각 브랜치가 독립 sprint 파일만 추가한다.
- 충돌 영역이 테이블 행 → 독립 파일로 분리되어 충돌이 실질적으로 사라진다.

### cost-log.md

**현재 (Phase A)**:
- `AI/cost-log.md`는 기존 방식으로 유지하되, DB primary인 `sdlc.ai_model_runs`를 병행 기록한다.
- 충돌 발생 시 해결 규칙: 스프린트 번호 순서대로 행을 정렬한다 (#N 먼저, #N+1 뒤).

**장기 (Phase B/C)**:
- `sdlc.ai_model_runs`가 primary가 되면, `AI/cost-log.md`는 DB에서 생성되는 report/export 파일로 전환한다.
- 브랜치가 직접 cost-log.md를 append하지 않는다 — 충돌 원인 자체가 제거된다.
- `generate_cost_log.py` (미구현, Phase B 시점에 도입) 스크립트로 DB → 파일 변환.

### workreport

- `AI/workreport/YYYY-MM-DD.md`는 날짜별 파일이므로 비교적 충돌 위험이 낮다.
- 같은 날짜에 여러 PR이 동시에 workreport를 수정하면 충돌 가능성이 있으므로,
  workreport는 하루 마지막에 한 번 main에서 직접 작성·커밋하는 방식을 권장한다.

## 충돌 발생 시 수동 해결 규칙

SPRINT.md 충돌:
```
<<<<<<< HEAD
## 스프린트 #N+1 ...
=======
## 스프린트 #N ...
>>>>>>> main
```
→ 두 섹션 모두 보존, 낮은 번호(#N)를 먼저, 높은 번호(#N+1)를 뒤에 배치.

cost-log.md 충돌:
```
<<<<<<< HEAD
| date | #N+1 | ...
=======
| date | #N | ...
>>>>>>> main
```
→ 두 행 모두 보존, 스프린트 번호 오름차순 정렬.

## 관련 문서

- [`AI/sprints/README.md`](../sprints/README.md): AI/sprints/ 구조 설명
- [`Docs/operations/ai-sdlc-db-migration-roadmap.md`](ai-sdlc-db-migration-roadmap.md): DB 전환 로드맵
