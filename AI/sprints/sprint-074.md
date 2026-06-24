---
sprint: 74
title: Gomoku E2E 자동화
branch: 2026-06-25_AddGomokuE2EAutomation
date: 2026-06-25
status: in-progress
---

# Sprint #74 — Gomoku E2E 자동화

## 목표

DummyClient에 `--e2e` CLI 모드를 추가하여 오목 E2E 테스트를 스크립트/CI에서 자동으로 실행할 수 있게 한다.

## 태스크

- [ ] DummyClient `Program.cs`에 `--e2e <번호>` / `--e2e all` / `--list` CLI 모드 추가
- [ ] `TeeWriter` 구현 — Console + File 동시 출력
- [ ] `logs/e2e-{yyyyMMdd-HHmmss}.log` 자동 생성
- [ ] `TwoPlayerGomokuScenario` 명시적 `bool` 반환으로 수정
- [ ] `scripts/run-e2e.sh` (bash) 실행 스크립트 작성
- [ ] `scripts/run-e2e.ps1` (PowerShell) 실행 스크립트 작성
- [ ] `logs/` 디렉토리 `.gitignore` 등록
- [ ] 빌드/테스트 통과 확인

## 배경

Sprint #72에서 `TwoPlayerGomokuScenario`가 구현되었다. 이번 스프린트에서는 이를 non-interactive 모드로 실행 가능하게 하여 CI 연동 및 반복 실행을 지원한다.

## 참조

- DB job: `sdlc.ai_jobs.branch = 2026-06-25_AddGomokuE2EAutomation`
- 계획 파일: `.claude/plan/2026-06-25_GomokuE2EAutomation.md`
- 관련 스프린트: Sprint #72 (GomokuE2EReadiness)
