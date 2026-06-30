---
sprint: 81
title: Library.Game 추상화 완성
branch: 2026-07-01_CompleteLibraryGameAbstraction
date: 2026-07-01
status: in-progress
---

# Sprint #81 — Library.Game 추상화 완성

## 목표
PlatformA.Library.Game에 게임 서버 공통 인터페이스와 추상 기반 클래스를 완성하여 Gomoku 이후 LegendHero·BattleWar 게임 서버가 상속·확장할 수 있는 구조를 만든다.

## 태스크
- [ ] 현재 Library.Game 구조 분석 (기존 3개 파일 파악)
- [ ] IGameSession 인터페이스 정의
- [ ] IGameRoom 인터페이스 정의
- [ ] IGameRoomManager 인터페이스 정의
- [ ] GameSessionBase 추상 기반 클래스 구현
- [ ] GameRoomBase 추상 기반 클래스 구현
- [ ] GameRoomManagerBase 추상 기반 클래스 구현
- [ ] Game.Gomoku의 기존 클래스를 추상 기반 클래스 상속으로 리팩토링
- [ ] 빌드·테스트 통과 확인

## 배경
현재 PlatformA.Library.Game은 완성도 30% (GameSession, GameRoom, GameRoomManager 파일 존재)이며
공통 인터페이스·추상 클래스가 없어 새 게임(LegendHero, BattleWar) 개발 시
코드 중복 및 구조 불일치 문제가 발생할 수 있다.
게임 로드맵(Gomoku → LegendHero → BattleWar) 진행을 위해 공통 인프라 완성이 필요하다.

## 참조
- DB job: `sdlc.ai_jobs.branch = 2026-07-01_CompleteLibraryGameAbstraction`
- 관련: project_direction.md — Library.Game (Option B-1 아키텍처)
