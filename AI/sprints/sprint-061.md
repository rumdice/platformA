---
sprint: 61
title: 워크플로 버그 수정 및 역할 분담 명세
branch: 2026-06-11_FixWorkflowAndRoleSeparation
date: 2026-06-11
status: done
completed: 2026-06-11
---

# Sprint #61 — 워크플로 버그 수정 및 역할 분담 명세

## 목표

`plan-file-trigger.yml` 문법 오류 수정, CLAUDE.md에 GitHub Actions/LLM/DB/n8n 역할 분담 명시.

## 태스크

- [x] `plan-file-trigger.yml` — `paths`와 `paths-ignore` 동시 사용 문법 오류 수정
- [x] `CLAUDE.md` — 시스템 역할 분담 섹션 추가 (GitHub Actions DB 접근 금지 원칙)
- [x] `CLAUDE.md` — "절대 하지 말 것"에 GitHub Actions DB 접근 및 코드 수정 금지 추가

## 배경

- `plan-file-trigger.yml`이 `paths`와 `paths-ignore`를 동일 이벤트에 동시 정의하여 L8 Col:5 오류 발생
- GitHub Actions(클라우드)가 로컬 PostgreSQL에 접근 시도하는 설계 패턴이 반복됨
- 역할 분담을 명시하여 동일 오류 재발 방지: GitHub=검증/빌드/배포, LLM=판단, DB=기억, n8n=흐름제어

## 참조

- DB job: `sdlc.ai_jobs.branch = 2026-06-11_FixWorkflowAndRoleSeparation`
- 원인 파일: `.github/workflows/plan-file-trigger.yml`
- 정책 파일: `CLAUDE.md`
