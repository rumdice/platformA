---
sprint: 80
title: /e2e 시나리오 9 TCP 헬스체크
branch: 2026-07-01_FixE2EScenario9TcpHealth
date: 2026-07-01
status: done
completed: 2026-07-01
pr: https://github.com/rumdice/platformA/pull/114
---

# Sprint #80 — /e2e 시나리오 9 TCP 헬스체크

## 목표
/e2e 스킬 시나리오 9의 Game.Gomoku 헬스체크를 HTTP에서 TCP 방식으로 변경하여 Windows 권한 문제로 인한 오탐을 제거한다.

## 태스크
- [ ] .claude/skills/e2e/SKILL.md 시나리오 9 헬스체크 코드 분석
- [ ] `http://localhost:7779/healthz` → `Test-NetConnection -Port 7778` TCP 체크로 변경
- [ ] /e2e 시나리오 9 실행으로 동작 확인

## 배경
PR #113(Sprint #79)에서 ServiceManager.cs의 Game.Gomoku 헬스체크를 TcpPort:7778로 수정했으나,
/e2e 스킬의 시나리오 9 구간에는 여전히 HTTP(`http://localhost:7779/healthz`)로 체크하는 코드가 남아 있음.
Windows HttpListener는 비관리자 권한으로 바인딩에 조용히 실패하므로 TCP 방식으로 통일이 필요하다.

## 참조
- DB job: `sdlc.ai_jobs.branch = 2026-07-01_FixE2EScenario9TcpHealth`
- 관련 PR: #113 (ServiceManager.cs TcpPort 수정)
