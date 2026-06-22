---
sprint: 69
title: Game.Lobby 구조 전환 및 Matching.API 완성도 향상
branch: 2026-06-22_GameLobbyAndMatchingUpgrade
date: 2026-06-22
status: done
completed: 2026-06-23
pr: https://github.com/rumdice/platformA/pull/102
---

# Sprint #69 — Game.Lobby 구조 전환 및 Matching.API 완성도 향상

## 목표

Game.Server를 Lobby 서버로 재구성하고 Matching.API 기능을 강화하여 매칭→게임 진입 플로우를 완성한다.

## 태스크

### A. Game.Lobby 프로젝트 전환
- [x] `PlatformA.Game.Server` → `PlatformA.Game.Lobby` 프로젝트 리네임 (csproj, namespace, sln)
- [x] `packets.proto`에 `CMatchRequest` / `SMatchFound` 패킷 추가
- [x] `Consts.cs`에 `GAME_TRANSFER_KEY_PREFIX`, `GOMOKU_SERVER_IP/PORT`, `PLAYER_RATING_KEY_PREFIX` 상수 추가
- [x] `PacketHandler`: `CMatchRequest` 핸들러 추가 (Matching.API HTTP 호출 → Redis game_transfer 티켓 발급 → SMatchFound 전송)
- [x] `PlatformA.Tests.Game.Server` → `PlatformA.Tests.Game.Lobby` 리네임 (56개 테스트 유지)

### B. Game.Gomoku 로그인 흐름 변경
- [x] `GomokuPacketHandler`: `CLogin` 핸들러 추가 (`game_transfer` 키 검증·소비 → GomokuRoom 입장)
- [x] `GomokuRoomManager` 추가 (string roomId 기반 방 관리)

### C. Matching.API 완성도 향상
- [x] `MatchRecord` Entity에 `GameType`, `RoomId`, `Player1Rating`, `Player2Rating` 컬럼 추가
- [x] EF Core Migration 생성 (`AddGameTypeAndRatingToMatchRecords`)
- [x] `TryMatchAsync`: gameType별 큐 + Lua 원자적 pop + MMR 조회 → 즉시 매칭 또는 대기열 추가
- [x] 매칭 완료 시 match_records INSERT + 상대방 game_transfer 티켓 발급
- [x] `POST /api/gamematch/request` 엔드포인트 추가 (Lobby 내부 호출용)
- [x] `GET /api/gamematch/history` 엔드포인트 추가 (플레이어 매칭 이력 조회)
- [x] `PlatformA.Tests.Matching.API` 테스트 12개 → 20개로 보강

## 배경

이전 아키텍처 논의에서 결정된 사항:
- Game.Gomoku / Game.CartRider 등 각 게임은 독립 프로세스로 순수 게임 로직만 담당
- Game.Server는 Lobby 서버로 전환: 모든 클라이언트의 첫 TCP 진입점, 로그인/매칭 요청/게임 서버 라우팅
- 세션 전환 방식: Matching.API가 Redis game_transfer 티켓 발급 → 클라이언트가 게임 서버에 직접 연결

## 플로우

```
Client → Game.Lobby (TCP :7777)
  CLogin → JWT 검증, 로비 입장
  CMatchRequest{gameType:"gomoku"}
    → Matching.API HTTP POST /api/matching/request
    → 매칭 완료: Redis game_transfer:{userId}={roomId, host, port} 발급
  SMatchFound{host, port:7778, roomId}
Client → Game.Gomoku (TCP :7778)
  CLogin{jwt, roomId} → game_transfer 키 확인 → 방 입장 → 게임 시작
```

## 참조

- DB job: `sdlc.ai_jobs.branch = 2026-06-22_GameLobbyAndMatchingUpgrade`
- 관련 논의: 2026-06-22 아키텍처 설계 세션
