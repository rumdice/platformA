---
sprint: 70
title: Game.Lobby SignalR 전환 및 아키텍처 정비
branch: 2026-06-23_LobbySignalRRedesign
date: 2026-06-23
status: in-progress
---

# Sprint #70 — Game.Lobby SignalR 전환 및 아키텍처 정비

## 목표

Game.Lobby를 TCP 서버에서 ASP.NET Core + SignalR 기반으로 전환하여 올바른 로비 서버 아키텍처를 완성한다.

## 태스크

### A. Game.Lobby 전면 재설계 (TCP → ASP.NET Core + SignalR)
- [ ] `Game.Lobby.csproj` 패키지 변경 (TCP 의존 제거, ASP.NET Core / SignalR / JWT 추가)
- [ ] `Program.cs` 전면 재작성 (ASP.NET Core WebApplication, SignalR, JWT 인증 등록)
- [ ] `Hubs/LobbyHub.cs` 생성 (OnConnectedAsync JWT 검증, RequestMatch, CancelMatch)
- [ ] `Services/LobbyPresenceService.cs` 생성 (온라인 유저 ConcurrentDictionary 추적)
- [ ] `Services/MatchNotificationService.cs` 생성 (Redis Pub/Sub 구독 → SignalR push)
- [ ] 구 TCP 코드 제거 (`Network/GameSession.cs`, `Packet/PacketHandler.cs`, `Network/LobbyHttpClientFactory.cs`)

### B. Matching.API 알림 보완
- [ ] `TryMatchAsync` 수정: 매칭 성사 시 두 플레이어 모두 Redis `channel:match_found`에 publish

### C. Game.Server 삭제
- [ ] `PlatformA.sln`에서 `PlatformA.Game.Server` 프로젝트 제거
- [ ] `PlatformA/PlatformA.Game.Server/` 디렉토리 삭제

### D. Tests.Game.Lobby 업데이트
- [ ] SignalR Hub 테스트로 교체 (WebApplicationFactory 패턴, JWT 인증 포함)
- [ ] 기존 TCP 패킷 테스트 제거 (PacketFraming, Move, EnterRoom 등)

## 배경

기존 Game.Lobby는 Game.Server를 리네임한 것으로 TCP + Protobuf 기반이었다.
설계 의도는 모든 클라이언트의 첫 진입점이 되는 로비 서버로, SignalR 기반의 상시 연결(StateFull)이 맞다.
JWT 로그인 → 매칭 신청 → 게임 서버 이동 → 다시 로비 복귀 플로우를 완성한다.

## 참조

- DB job: `sdlc.ai_jobs.branch = 2026-06-23_LobbySignalRRedesign`
- 관련 결정: 2026-06-23 아키텍처 정비 세션
