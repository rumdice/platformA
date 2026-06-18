---
sprint: 67
title: gate-check n8n 브리지 전환
branch: 2026-06-18_MigrateGateCheckToN8n
date: 2026-06-18
status: done
completed: 2026-06-18
pr: https://github.com/rumdice/platformA/pull/99
---

# Sprint #67 — gate-check n8n 브리지 전환

## 목표

GitHub Actions의 gate-check를 파일 기반에서 n8n 브리지 방식으로 전환한다.
GitHub Actions는 n8n webhook으로 신호만 전달하고, n8n이 로컬 PostgreSQL에서 게이트 상태를 조회한 뒤 GitHub Commit Status API로 결과를 세팅한다.

## 태스크

- [x] `.github/workflows/sdlc-gate-check.yml` — n8n webhook 호출로 교체
- [x] `check_sdlc_gate.py` — deprecated 마킹 (로직 보존, 롤백 가능)
- [x] n8n 워크플로우 명세 작성 (DB 조회 → Commit Status 세팅)
- [x] GitHub branch protection rule — `AI_SDLC/gate-check` 커밋 상태 체크 설정 가이드 작성
- [x] `N8N_WEBHOOK_URL` GitHub Actions secret 등록 가이드 문서화

## 배경

Sprint #66 PR #98에서 gate-check CI가 실패했다. 원인: Phase C 스프린트는 `AI/tasks/*.json`을 생성하지 않는데, `check_sdlc_gate.py`는 파일 기반으로만 gate를 검사한다. GitHub Actions는 로컬 PostgreSQL에 접근할 수 없으므로(CLAUDE.md 원칙) n8n 브리지를 통해 해결한다.

## 아키텍처

```
PR opened
  → GitHub Actions: POST n8n_webhook {branch, sha, pr}
  → n8n: SELECT * FROM sdlc.ai_jobs WHERE branch = $1
  → n8n: POST /repos/rumdice/platformA/statuses/{sha}
         { state: "success"|"failure", context: "AI_SDLC/gate-check" }
  → GitHub PR: commit status 표시 → merge 허용/차단
```

## 참조

- DB job: `sdlc.ai_jobs.branch = 2026-06-18_MigrateGateCheckToN8n`
- 관련 이슈: Sprint #66 PR #98 gate-check CI 실패
- 아키텍처 원칙: CLAUDE.md "GitHub Actions ↔ DB 접근 금지 원칙"
