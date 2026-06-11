# AI SDLC 개요

PlatformA AI SDLC(Software Development Life Cycle)는 Claude Code CLI를 핵심으로 한
AI 기반 개발 자동화 시스템입니다.

## 목적

반복적인 개발 작업(요구사항 분석, 코딩, 테스트 생성, 리뷰, PR 생성)을 AI가 수행하고
사람은 설계 결정과 PR 검수·머지만 담당하는 구조입니다.

## 핵심 구성 요소

| 구성 요소 | 역할 |
|---|---|
| Claude Code CLI | 코딩 에이전트 (`.claude/skills/` 스킬로 제어) |
| PostgreSQL `platforma_sdlc` | 작업 상태·이력 단일 진실 공급원 |
| n8n | CI 실패 감지 → auto-fix dispatch 오케스트레이터 |
| GitHub Actions | CI/CD + auto-fix 실행 환경 |

## 워크플로 한 줄 요약

```
사용자: /workflow 작업설명
  → /plan  → /requirement → /impact → /start → (코딩) → /test-gen → /done → /review → /pr
  → GitHub에서 PR 검토 후 머지
```

## 현재 Phase

**Phase C — DB 단독 운영** (2026-06-10 ~)

- task JSON 파일 신규 쓰기 중단, DB `sdlc.ai_jobs`가 단일 진실 공급원
- 모든 게이트 판정(requirement_done, impact_done, test_generated, review_completed)을 DB에서 읽음
- SPRINT.md는 PR 머지 후 `generate_sprint_md.py`가 DB 기반으로 자동 재생성

## 문서 구성

| 문서 | 내용 |
|---|---|
| [Workflow](workflow.md) | 전체 파이프라인 흐름 |
| [Phase A/B/C](phases.md) | DB 마이그레이션 로드맵 |
| [Phase C DB 단독 운영](phase-c-db-only.md) | 현재 운영 방식 상세 |
| [Gate 판정](gates.md) | 각 스킬 통과 조건 |
| [DB Schema](db-schema.md) | `sdlc.*` 테이블 명세 |
| [Job Lock](job-lock.md) | 동시 실행 방지 메커니즘 |
| [Cost & Reports](cost-and-reports.md) | 토큰 비용 추적 |
| [Auto Fix](auto-fix.md) | CI 실패 자동 수정 정책 |
| [n8n](n8n.md) | 실패 모니터 워크플로 |
| [GitHub Actions](github-actions.md) | CI/CD + docs 파이프라인 |
| [Backup / Restore](backup-restore.md) | DB 백업·복구 절차 |
| [Troubleshooting](troubleshooting.md) | 문제 해결 가이드 |
| [Skill Reference](skill-reference.md) | 스킬 목록 (자동 생성) |
| [Automation Map](automation-map.md) | 자동화 워크플로 맵 (자동 생성) |
