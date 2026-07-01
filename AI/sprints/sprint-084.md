---
sprint: 84
title: GomokuRoom 핵심 게임 로직 단위 테스트
branch: 2026-07-02_AddGomokuRoomTests
date: 2026-07-02
status: done
completed: 2026-07-02
pr: https://github.com/rumdice/platformA/pull/118
---

# Sprint #84 — GomokuRoom 핵심 게임 로직 단위 테스트

## 목표
GomokuRoom의 HandleDisconnect·HandlePlaceStone·FinishGame 로직에 대한 단위 테스트를 추가하여 E2E에만 의존하는 커버리지 공백을 해소한다.

## 태스크
- [x] `Helpers/FakeGomokuSession.cs` 생성 — GomokuSession 상속, SessionId 수동 설정
- [x] `Helpers/TestableGomokuRoom.cs` 생성 — GomokuRoom 상속, Broadcast() override로 패킷 캡처
- [x] `GomokuRoomDisconnectTests.cs` — HandleDisconnect 3케이스 (WaitingPlayers, InProgress, Finished) 테스트 5개
- [x] `GomokuRoomPlaceStoneTests.cs` — HandlePlaceStone (턴 검증, 5목, 무승부 등) 테스트 8개
- [x] `GomokuRoomFinishGameGuardTests.cs` — FinishGame 중복 호출 가드 + StartGame 브로드캐스트 테스트 2개
- [x] `dotnet test PlatformA.sln -q` 전체 통과 (기존 52 + 신규 15 = 67개)
- [x] `.claude/rules/tests.md` 테스트 수 현황 업데이트

## 배경
52개 기존 테스트는 WinChecker·TurnManager·Board·GomokuRoomManager를 커버하지만
GomokuRoom 오케스트레이션 자체(연결 끊김→승패, 5목 감지→게임 종료, 무승부 등)는 테스트 전무.
TestableGomokuRoom(Broadcast 캡처) + FakeGomokuSession(TCP 없는 세션)으로 해소.

## 참조
- DB job: `sdlc.ai_jobs.branch = 2026-07-02_AddGomokuRoomTests`
- 계획 파일: `~/.claude/plans/7-1-flickering-cook.md`
