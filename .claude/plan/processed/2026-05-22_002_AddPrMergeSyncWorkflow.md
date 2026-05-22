# 요구사항 명세 — PR 머지 자동 감지 워크플로우

**작성일**: 2026-05-22  
**스프린트**: #26  
**브랜치**: 2026-05-22_AddPrMergeSyncWorkflow  
**상태**: 처리 완료 (소급 작성 — Plan mode 승인 후 /requirement 누락으로 사후 생성)

---

## 1. 배경 및 목적

현재 SDLC 파이프라인에서 `/pr` 스킬은 PR 생성과 동시에 task JSON·SPRINT.md를 갱신하지만,
사용자가 GitHub에서 PR을 머지한 사실을 Claude Code가 자동으로 감지하지 못한다.

**구체적 문제 사례**: PR #52 (test-match 제거) 머지 후 사용자가 직접
"머지 완료했다"고 고지해야 Claude Code가 상태를 파악할 수 있었다.

**목표**: GitHub Actions를 통해 PR 머지 이벤트를 감지하고
task JSON · SPRINT.md · cost-log를 자동으로 동기화한다.

---

## 2. 요구사항

### 기능 요구사항

| ID | 요구사항 | 우선순위 |
|----|----------|----------|
| F-01 | PR이 main에 머지될 때 해당 브랜치의 task JSON을 탐색한다 | P0 |
| F-02 | task JSON `status` → `"done"`, `completed_at`, `pr_url` 자동 갱신 | P0 |
| F-03 | SPRINT.md 해당 스프린트 섹션의 `- [ ]` → `- [x]` 자동 처리 | P0 |
| F-04 | `/pr` 스킬 미실행 경로에서만 cost-log.md 행 추가 | P1 |
| F-05 | task JSON 없는 브랜치(핫픽스 등)는 에러 없이 skip | P0 |

### 비기능 요구사항

- **Idempotency**: `/pr` 스킬이 이미 실행된 경우(status="done") 중복 갱신 없음
- **안전성**: 워크플로우 실패가 PR 머지를 block하지 않음
- **가시성**: 각 처리 단계를 `[ok]` / `[skip]` 로그로 출력

---

## 3. 설계 방향

### 구현 방식: GitHub Actions + Python 스크립트

```
pull_request (types: [closed], branches: [main])
  if: merged == true
    → python3 .github/scripts/sync_merged_pr.py
    → git commit & push (변경 있을 때만)
```

Python 스크립트 선택 이유:
- JSON 파싱·수정이 bash/jq보다 안정적
- 정규식 기반 SPRINT.md 섹션 탐색 가능
- GitHub Actions ubuntu-latest에 Python 3.12 기본 내장

### 스크립트 처리 흐름

```
BRANCH 환경변수 → AI/tasks/sprint*.json 순회
  → branch 필드 일치 파일 탐색
  → (없으면 exit 0)
  → status != "done" 이면: JSON 갱신 (was_pending=True)
  → sprint 번호로 SPRINT.md 섹션 찾기 → [ ]→[x]
  → was_pending==True 이면: cost-log 행 추가
```

---

## 4. 영향 범위

| 변경 파일 | 유형 | 영향 |
|-----------|------|------|
| `.github/workflows/pr-merge-sync.yml` | 신규 | PR 머지 시마다 실행 |
| `.github/scripts/sync_merged_pr.py` | 신규 | 워크플로우에서 호출 |
| `AI/tasks/*.json` | 런타임 수정 | 각 PR 머지 후 자동 갱신 |
| `AI/SPRINT.md` | 런타임 수정 | 각 PR 머지 후 자동 갱신 |
| `AI/cost-log.md` | 런타임 수정 | /pr 미실행 시에만 갱신 |

기존 CI(`ci.yml`, `docs.yml`)와 트리거 조건이 다르므로 충돌 없음.

---

## 5. 검증 기준

- [ ] PR 머지 후 Actions 탭에서 "PR Merge — SDLC Task Sync" 워크플로우 실행 확인
- [ ] `AI/tasks/*.json` → `status: "done"`, `pr_url` 채워짐
- [ ] `AI/SPRINT.md` → 해당 스프린트 `- [x]` 확인
- [ ] task JSON 없는 브랜치 머지 시 → 워크플로우 성공 종료 (skip 로그)
- [ ] `/pr` 스킬 실행 후 머지 시 → 중복 갱신 없음 확인
- [ ] **자기 검증**: 이 PR(#53) 머지 시 sprint26 JSON이 자동으로 done 처리됨
