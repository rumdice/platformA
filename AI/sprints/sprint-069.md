---
sprint: 69
title: Game.Lobby 구조 전환 및 Matching.API 완성도 향상
branch: 2026-06-22_GameLobbyAndMatchingUpgrade
date: 2026-06-22
status: in-progress
---

# Sprint #69 — Game.Lobby 구조 전환 및 Matching.API 완성도 향상

## 목표

Game.Server를 Lobby 서버로 재구성하고 Matching.API 기능을 강화하여 매칭→게임 진입 플로우를 완성한다.

## 태스크

### A. Game.Lobby 프로젝트 전환
- [ ] `PlatformA.Game.Server` → `PlatformA.Game.Lobby` 프로젝트 리네임 (csproj, namespace, Dockerfile, sln)
- [ ] `packets.proto`에 `CMatchRequest` / `SMatchFound` 패킷 추가
- [ ] `Consts.cs`에 `GAME_TRANSFER_KEY_PREFIX` 상수 추가
- [ ] `PacketHandler`: `CMatchRequest` 핸들러 추가 (Matching.API HTTP 호출 → Redis game_transfer 티켓 발급 → SMatchFound 전송)
- [ ] `PlatformA.Tests.Game.Server` → `PlatformA.Tests.Game.Lobby` 리네임 + Lobby 핸들러 테스트 추가

### B. Game.Gomoku 로그인 흐름 변경
- [ ] `GomokuPacketHandler` (또는 별도 login 핸들러): `active_user_key` 대신 `game_transfer` 키 확인으로 교체
- [ ] Game.Gomoku에 `CLogin` 패킷 핸들러 추가 (현재 없으면)

### C. Matching.API 완성도 향상
- [ ] `match_records` DB 테이블 Entity 정의 + `DbWebAppContext` 등록
- [ ] EF Core Migration 생성 및 적용 (`CreateMatchRecordsTable`)
- [ ] MMR 기반 매칭 알고리즘 구현 (간단한 ELO 점수 기반 범위 매칭)
- [ ] 매칭 완료 시 match_records INSERT + 양측 플레이어 game_transfer 티켓 발급
- [ ] `GET /api/matching/history` 엔드포인트 추가 (플레이어 매칭 이력 조회)
- [ ] `PlatformA.Tests.Matching.API` 테스트 12개 → 25개 이상으로 보강

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
