# 요구사항 명세 — AI_SDLC GitHub Actions Gate 강화 및 자동 리포트

**작성일**: 2026-05-26
**스프린트**: #27
**브랜치**: 2026-05-26_SdlcGateCheckAndReport
**상태**: 처리 완료

---

## 1. 배경 및 목적

스프린트 #26까지 AI_SDLC의 게이트 검사는 Claude Code `/pr` 스킬 내부에만 존재한다.
사용자가 `/pr`을 우회하거나 GitHub UI에서 직접 PR을 만들면 게이트를 건너뛸 수 있다.

**목표**: GitHub Actions를 통해 PR 생성 시 자동으로 SDLC 공정 준수 여부를 검사하고,
`sync_merged_pr.py`를 강화하며, JSON/Markdown 기반 주간 리포트 스크립트를 추가한다.

---

## 2. 요구사항

### 기능 요구사항

| ID | 요구사항 | 우선순위 |
|----|----------|----------|
| F-01 | PR 생성/업데이트 시 GitHub Actions가 SDLC 게이트를 자동 검사한다 | P0 |
| F-02 | task JSON 없는 PR(핫픽스/문서)은 warning 처리, 실패 아님 | P0 |
| F-03 | 코드 변경 + test_generated=false → FAIL | P0 |
| F-04 | 고위험 경로 변경 + impact=null → FAIL | P0 |
| F-05 | 고위험 경로 변경 + review_completed=false → FAIL | P0 |
| F-06 | sync_merged_pr.py에 steps[] merge_sync 기록 추가 | P1 |
| F-07 | cost-log 중복 방지 (동일 task name 이미 있으면 skip) | P1 |
| F-08 | GitHub Actions Job Summary에 동기화 결과 출력 | P1 |
| F-09 | 주간 SDLC 리포트 생성 스크립트 추가 (수동 실행) | P2 |
| F-10 | SCHEMA.md, AI_SDLC(pipeline).txt 최신화 | P1 |

### 비기능 요구사항

- **비파괴적 게이트**: sdlc-gate-check.yml 실패가 PR 머지를 block하지 않음 (Required check 미등록)
- **Idempotency**: 이미 done 상태이거나 cost-log에 기록된 경우 중복 갱신 없음
- **가시성**: GitHub Actions Job Summary에 검사 결과 명시

---

## 3. 영향 범위

| 변경 파일 | 유형 | 영향 |
|-----------|------|------|
| `.github/workflows/sdlc-gate-check.yml` | 신규 | PR open/sync 시 실행 |
| `.github/scripts/check_sdlc_gate.py` | 신규 | 게이트 검사 로직 |
| `.github/scripts/sync_merged_pr.py` | 수정 | steps[], cost-log 중복 방지, summary |
| `.github/scripts/generate_sdlc_report.py` | 신규 | 주간 리포트 생성 |
| `AI/reports/README.md` | 신규 | 리포트 디렉토리 설명 |
| `AI/tasks/SCHEMA.md` | 수정 | GitHub Actions 연동 섹션 추가 |
| `AI/AI_SDLC(pipeline).txt` | 수정 | 단계 10, 11 추가 |
| `AI/SPRINT.md` | 수정 | 스프린트 #27 추가 |

---

## 4. 검증 기준

- [ ] PR 생성 시 "AI_SDLC Gate Check" 워크플로우 실행 확인
- [ ] 코드 변경 + test_generated=false PR → FAIL 확인
- [ ] 문서-only PR → PASS 확인
- [ ] task JSON 없는 브랜치 PR → WARNING (실패 아님) 확인
- [ ] PR 머지 후 steps[]에 merge_sync 기록 확인
- [ ] 동일 task의 cost-log 중복 행 미생성 확인
- [ ] `python3 .github/scripts/generate_sdlc_report.py` 실행 후 `AI/reports/weekly_*.md` 생성 확인
