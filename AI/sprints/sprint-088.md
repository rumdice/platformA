---
sprint: 88
title: Matching.API 직접 접근 제거
branch: 2026-07-09_RemoveMatchingDirectAccess
date: 2026-07-09
status: done
completed: 2026-07-09
pr: https://github.com/rumdice/platformA/pull/122
---

# Sprint #88 — Matching.API 직접 접근 제거

## 목표
구 아키텍처 잔재인 클라이언트→Matching.API 직접 호출 경로를 제거하여 모든 상태 변경이 Lobby를 경유하도록 정리한다.

## 태스크
- [x] Matching.API: MatchingHub.cs 삭제 + Program.cs MapHub 제거
- [x] Matching.API: RequestMatch(Deprecated) 엔드포인트 제거
- [x] Matching.API: CancelMatch(JWT) → POST /cancel (내부 전용) 교체
- [x] Matching.API: GetStatus(JWT) → GET /status/{userId} (내부 전용) 교체
- [x] LobbyHub: JWT 포워딩 방식 → 내부 엔드포인트 호출로 교체
- [x] DummyClient: LoadTestMatchingScenario 삭제 (Scenario 10 대체)
- [x] DummyClient: MatchingScenario → Lobby SignalR 경유로 재작성
- [x] GameMatchControllerTests: 삭제된 엔드포인트 테스트 제거
- [x] Consts.cs: MATCH_API_URL, MATCH_HUB_URL 상수 제거

## 배경
Game.Lobby 도입 전 클라이언트가 Matching.API에 직접 연결하던 구조의 잔재.
현재 아키텍처는 Client→Lobby(SignalR)→Matching.API(내부HTTP) 경유이며,
MatchingHub, RequestMatch, JWT 인증 CancelMatch/Status는 더 이상 사용되지 않는다.
읽기 전용 엔드포인트(history, rating)는 유지한다.

## 참조
- DB job: `sdlc.ai_jobs.branch = 2026-07-09_RemoveMatchingDirectAccess`
