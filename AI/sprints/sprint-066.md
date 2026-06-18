---
sprint: 66
title: 게임 라이브러리 추출 및 Gomoku 프로젝트 생성
branch: 2026-06-18_CreateGameLibGomoku
date: 2026-06-18
status: done
completed: 2026-06-18
pr: https://github.com/rumdice/platformA/pull/98
---

# Sprint #66 — 게임 라이브러리 추출 및 Gomoku 프로젝트 생성

## 목표

게임 서버 아키텍처를 Option B-1로 전환한다. Game.Server의 인프라 코드를 PlatformA.Library.Game으로 추출하고, 오목 게임 로직을 담는 PlatformA.Game.Gomoku 신규 프로젝트를 생성한다.

## 태스크

- [x] `PlatformA.Library.Game` 프로젝트 생성 (csproj, PlatformA.Library 참조)
- [x] `GameSession`, `GameRoom`, `GameRoomManager` Game.Server → Library.Game으로 이동
- [x] `PlatformA.Game.Server` — Library.Game 참조로 교체, 중복 코드 제거
- [x] `PlatformA.Game.Gomoku` 프로젝트 생성 (Library.Game 참조)
- [x] Gomoku 전용 패킷 정의 (`packets.proto`: CPlaceStone, SBoardUpdate, SGameOver 등)
- [x] `Board.cs` — 15×15 바둑판, 돌 놓기, 상태 조회
- [x] `WinChecker.cs` — 가로/세로/대각선 5연속 판정
- [x] `TurnManager.cs` — 플레이어 교대, 타임아웃
- [x] `GomokuRoom.cs` — GameRoom 상속, 게임 상태 관리
- [x] `GomokuPacketHandler.cs` — CPlaceStone 처리
- [x] `PlatformA.sln` — 신규 프로젝트 2개 등록
- [x] 빌드 및 테스트 통과

## 배경

게임 서버를 장르별 독립 프로세스로 분리하는 Option B-1 아키텍처 채택 (2026-06-18).
ASP.NET Core가 HTTP API 서버의 중간 레이어 역할을 하듯, PlatformA.Library.Game이
TCP 게임 서버들의 공통 인프라 레이어 역할을 담당한다.

로드맵:
- PlatformA.Game.Gomoku   ← 현재 (캐주얼 PvP)
- PlatformA.Game.LegendHero (MOBA)
- PlatformA.Game.BattleWar  (FPS)

## 참조

- DB job: `sdlc.ai_jobs.branch = 2026-06-18_CreateGameLibGomoku`
- 관련 ADR: ADR-007 (Protobuf 패킷)
- 아키텍처 결정: 2026-06-18 대화 (Option B-1 확정)
