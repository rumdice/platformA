# AI_SDLC Gate Check — 미해결 이슈 및 향후 도입 가이드

**상태**: 비활성화 (Sprint #67, 2026-06-18)
**관련 PR**: #99
**관련 파일**: `.github/scripts/check_sdlc_gate.py` (로직 보존)

---

## 왜 제거했는가

### 문제 1: Phase C(DB-only) 와 파일 기반 gate-check 충돌

- Sprint #52 이후 Phase C는 `AI/tasks/*.json`을 생성하지 않는다 (DB-only)
- `check_sdlc_gate.py`는 `AI/tasks/*.json`만 읽음
- Phase C 스프린트의 고위험 코드 변경 PR은 항상 FAIL → 수동으로 task JSON을 생성해야 했음
- 발견: Sprint #66 PR #98 gate-check CI 실패

### 문제 2: GitHub Actions가 로컬 PostgreSQL에 접근 불가

- GitHub Actions는 클라우드 runner (CLAUDE.md "GitHub Actions ↔ DB 접근 금지 원칙")
- 로컬 PostgreSQL은 개발자 PC 내부에 있어 직접 연결 불가
- 해결 시도: n8n 브리지 (GitHub Actions → n8n webhook → PostgreSQL → GitHub Commit Status API)

### 문제 3: n8n 브리지의 운영 부담

n8n 브리지 방식이 해결가능하지만 아래 운영 부담이 발생:

| 요구사항 | 현실적 문제 |
|---------|-----------|
| n8n이 공개 URL에서 실행 중이어야 함 | PC가 꺼지거나 절전되면 PR 머지 불가 |
| Cloudflare Tunnel / ngrok 상시 실행 | 로컬 프로세스 하나를 항상 유지해야 함 |
| Quick tunnel은 재시작 시 URL 변경 | GitHub Secret을 매번 업데이트해야 함 |

---

## 현재 상태

- `sdlc-gate-check.yml`: **삭제됨** — gate-check CI 없음
- `check_sdlc_gate.py`: **보존됨** — 향후 재도입 시 재사용 가능
- Branch protection: gate-check required status check 없음 (수동 머지 가능)

---

## 향후 도입 방안

### 방안 A: task JSON 재도입 (권장 — 비용 0, 인프라 없음)

Phase C에서도 gate 신호 전용 최소 task JSON을 `/plan` 시 자동 생성한다.
DB가 primary source of truth이지만, gate-check 플래그는 파일에도 미러링한다.

```json
// AI/tasks/sprint{N}_{PlanName}.json (최소 구조)
{
  "sprint": N,
  "branch": "브랜치명",
  "test_generated": false,
  "review_completed": false,
  "impact": null,
  "adr_required": false
}
```

- `/test-gen`, `/review`, `/impact` 실행 시 DB와 동시에 이 파일도 업데이트
- `check_sdlc_gate.py` 수정 없이 그대로 재활용
- 필요 인프라: 없음

**구현 범위**: `/plan` 스킬에서 task JSON 생성 로직 재추가 (Phase B 로직 복원)

### 방안 B: n8n 브리지 (인프라 필요)

n8n이 항상 접근 가능한 환경(클라우드 배포 또는 안정적 터널)이 확보된 경우에만 현실적.

- n8n을 Render.com / Railway 등 클라우드에 배포 (무료 티어 존재)
- `AI/docs/gate-check-setup.md` 내용 참조 (git history에 보존됨)
- 이 방식은 멀티유저 환경이나 CI/CD 완전 자동화 시 가장 적합

### 방안 C: GitHub Actions ephemeral DB (CI 전용)

GitHub Actions runner에서 PostgreSQL 컨테이너를 띄우고 상태를 파일로 직렬화하여 복원.
구현 복잡도가 높아 현재 규모에서는 과함.

---

## 재도입 시 체크리스트

- [ ] 방안 선택 (A/B/C)
- [ ] `sdlc-gate-check.yml` 복원 또는 신규 작성
- [ ] `check_sdlc_gate.py` 연결 (방안 A/C) 또는 n8n 설정 (방안 B)
- [ ] GitHub branch protection rule: Required status checks에 `gate-check` 또는 `AI_SDLC/gate-check` 추가
- [ ] 통합 테스트: 고위험 PR에서 gate-check FAIL → 수정 후 PASS 흐름 검증
