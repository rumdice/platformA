---
sprint: 72
title: Gomoku E2E 준비 코드 수정 및 시나리오
branch: 2026-06-24_GomokuE2EReadiness
date: 2026-06-24
status: in-progress
---

# Sprint #72 — Gomoku E2E 준비: 코드 수정 + 시나리오

## 목표

Gomoku E2E 실행 전 필수 버그 5건을 수정하고 전체 흐름(Auth→Lobby→Matching→Gomoku→GameOver→ResultRecord)을 자동으로 검증하는 TwoPlayerGomokuScenario를 구현한다.

## 태스크

- [ ] P0-A: Redis publish try/catch 추가 (GameMatchService.TryMatchAsync)
- [ ] P0-B: 구 BackgroundService 매칭 루프 비활성화 (ProcessQueueAsync/ProcessMatchingAsync 제거)
- [ ] P0-C: gameType 검증 추가 (GomokuPacketHandler.ProcessLoginAsync)
- [ ] P1-D: MatchNotificationService ProcessMatchFoundAsync 분리 (테스트 가능성 확보)
- [ ] P1-E: MatchHistory 무승부 표시 오류 수정 (WinnerId==null → "무승부")
- [ ] P1-F: TwoPlayerGomokuScenario 구현 (DummyClient 시나리오 9)
- [ ] 테스트 추가 및 전체 빌드/테스트 통과

## 배경

Gap analysis(platforma_gomoku_e2e_gap_analysis_2026-06-24.md) 및 코드 직접 검토 결과:
- GameMatchService.TryMatchAsync의 Redis publish 구간에 try/catch 없음
- GameMatchService : BackgroundService의 구 단일 큐 폴링 루프가 여전히 200ms마다 실행 중
- GomokuPacketHandler에서 game_transfer 티켓의 gameType을 검증하지 않음
- MatchNotificationService의 OnMatchFound가 async void 인라인으로 테스트 불가
- GetMatchHistoryAsync에서 무승부(WinnerId=null, Status=Completed)를 "미완료"로 표시
- 전체 흐름을 자동으로 검증하는 E2E 시나리오 없음

## 참조

- DB job: `sdlc.ai_jobs.branch = 2026-06-24_GomokuE2EReadiness`
- Gap analysis: `c:\Users\rumdi\Downloads\platforma_gomoku_e2e_gap_analysis_2026-06-24.md`
- 계획 파일: `.claude/plan/2026-06-24_GomokuE2EReadiness.md`
