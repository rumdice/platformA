---
sprint: 67
title: gate-check n8n 브리지 전환
branch: 2026-06-18_MigrateGateCheckToN8n
date: 2026-06-18
status: in-progress
---

# Sprint #67 — gate-check n8n 브리지 전환

## 목표

GitHub Actions gate-check를 파일 기반에서 n8n 브리지 방식으로 전환하여 Phase C(DB 전용) 스프린트에서 발생하는 CI 차단 문제를 근본적으로 해결한다.

## 태스크

- [ ] n8n 워크플로우 구성 — `gate-check` webhook 수신 → PostgreSQL 조회 → GitHub Commit Status API 호출
- [ ] `.github/workflows/sdlc-gate-check.yml` 수정 — `check_sdlc_gate.py` 실행 제거, n8n webhook 신호 전달로 교체
- [ ] GitHub branch protection rule 수정 — 기존 `gate-check` 체크 → n8n이 세팅하는 `AI_SDLC/gate-check` 커밋 상태로 교체
- [ ] `check_sdlc_gate.py` 파일 제거 또는 deprecated 처리
- [ ] n8n 공개 URL 환경변수 설정 (`N8N_WEBHOOK_URL`)
- [ ] 전체 플로우 통합 테스트 (PR 오픈 → n8n gate 조회 → 상태 반영 확인)

## 배경

Sprint #66(PR #98) 머지 시 gate-check FAIL 발생.
원인: Phase C 스프린트는 `AI/tasks/*.json`을 생성하지 않는데,
`check_sdlc_gate.py`는 파일만 읽어 gate 상태를 판단 → task JSON 없으면 고위험 변경 PR 차단.

GitHub Actions는 로컬 PostgreSQL 접근 불가(CLAUDE.md 원칙).
n8n 브리지를 통해 DB 조회 결과를 GitHub Commit Status로 세팅하는 것이 올바른 해결책.

## 플로우

```
PR 오픈/업데이트
  → GitHub Actions: n8n webhook으로 {branch, sha, pr} 전달
  → n8n: PostgreSQL에서 gate 상태 조회
  → n8n: GitHub API POST /repos/.../statuses/{sha}
      state: "success" | "failure"
      context: "AI_SDLC/gate-check"
  → GitHub PR: 커밋 상태 반영 → 머지 가능/불가 결정
```

## 선행 조건

- n8n이 GitHub Actions에서 접근 가능한 공개 URL에 노출되어야 한다
  (ngrok, cloudflare tunnel, 또는 클라우드 배포)
- GitHub Personal Access Token (repo 스코프) — n8n에서 Commit Status API 호출용

## 참조

- DB job: `sdlc.ai_jobs.branch = 2026-06-18_MigrateGateCheckToN8n`
- 관련 이슈: Sprint #66 PR #98 gate-check FAIL
- CLAUDE.md: "GitHub Actions ↔ DB 접근 금지 원칙"
