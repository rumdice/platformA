---
sprint: 79
title: Game.Gomoku E2E 헬스체크 수정
branch: 2026-06-30_FixGomokuE2EHealthCheck
date: 2026-06-30
status: done
completed: 2026-06-30
pr: https://github.com/rumdice/platformA/pull/113
---

# Sprint #79 — Game.Gomoku E2E 헬스체크 수정

## 목표
ServiceManager의 Game.Gomoku 헬스체크를 HTTP(7779)에서 TCP(7778)로 전환하여
E2E 시나리오 10 안정 실행을 보장한다.

## 태스크
- [x] ServiceManager.cs: Game.Gomoku spec에 TcpPort: 7778 추가
- [x] Program.cs: HttpListener 바인딩 http://+:7779/ → http://localhost:7779/ 변경
- [x] E2E 시나리오 10 실행 및 검증 (PASS)
- [x] Docs/e2e/2026-06-30.md 결과 문서 저장

## 배경
HttpListener가 Windows 비관리자 권한에서 `http://localhost:7779/` 바인딩에 조용히 실패.
기존 KillByPort도 7779 포트 기준으로 종료하여 실제 7778 포트 프로세스가 남는 문제 발생.
TcpPort: 7778 한 줄 추가로 PingAsync와 KillByPort 모두 TCP 기반으로 전환.

## 참조
- DB job: `sdlc.ai_jobs.branch = 2026-06-30_FixGomokuE2EHealthCheck`
- E2E 결과: `Docs/e2e/2026-06-30.md`
