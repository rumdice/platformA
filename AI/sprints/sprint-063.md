---
sprint: 63
title: SDLC CI 격리 강화
branch: 2026-06-12_FixSdlcCiIsolation
date: 2026-06-12
status: in-progress
---

# Sprint #63 — SDLC CI 격리 강화

## 목표

GitHub Actions → 로컬 DB 격리 위반 제거 및 n8n CI 실패 감지 체계를 커서 기반으로 강화하여 노트북 재시작 후에도 과거 실패를 소급 처리한다.

## 태스크

- [ ] `.github/workflows/auto-fix.yml` 삭제 — ANTHROPIC_API_KEY 과금 워크플로우 제거
- [ ] `.github/workflows/pr-merge-sync.yml` — "Sync sprint files (DB-based)" step 제거
- [ ] `.github/scripts/generate_sprint_md.py` 삭제 — 대상 파일(AI/SPRINT.md) 없음, DB 접근 코드 포함
- [ ] `.n8n/workflows/github-failure-monitor.json` — 15분 필터 → 커서 기반 폴링으로 교체, dispatch 노드 5개 제거
- [ ] `.claude/skills/workflow/SKILL.md` — 0.5단계 미해결 CI 실패 알림 추가
- [ ] `.claude/hooks/session-start.sh` — main 브랜치일 때 전체 미해결 CI 실패 표시 추가

## 배경

- Sprint #62(PR #94) 머지 후 AI SDLC 완성도 약 88% 평가
- `pr-merge-sync.yml`이 `generate_sprint_md.py`를 호출하며 psycopg2로 로컬 DB 접근 시도 → 격리 원칙 위반
- `AI/SPRINT.md`는 Phase C에서 삭제됨 → `generate_sprint_md.py`는 이중으로 불필요
- n8n 15분 타임 필터로 인해 노트북 OFF 중 발생한 CI 실패를 재시작 후에도 감지 못함
- `auto-fix.yml`은 ANTHROPIC_API_KEY 미등록으로 실행 불가 상태로 방치

## 참조

- DB job: `sdlc.ai_jobs.branch = 2026-06-12_FixSdlcCiIsolation`
- 계획 파일: `C:\Users\rumdi\.claude\plans\hazy-booping-moore.md`
- 관련 PR: #94 (Phase C 참조 정리)
