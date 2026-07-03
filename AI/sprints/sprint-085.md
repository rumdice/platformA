---
sprint: 85
title: ELO MMR 기반 매칭 구현
branch: 2026-07-03_AddEloMmrMatching
date: 2026-07-03
status: done
completed: 2026-07-03
pr: https://github.com/rumdice/platformA/pull/119
---

# Sprint #85 — ELO MMR 기반 매칭 구현

## 목표
TryMatchAsync를 ELO 레이팅 기반 3단계 범위 매칭(TTL 기반 대기시간 추적)으로 전환하고,
K-factor 감소로 MMR 희석을 방지한다. ELO 통합 테스트 5개 추가.

## 태스크
- [x] `Consts.cs` 상수 4개 추가 (MATCH_RATING_RANGE, MID, WIDE, WAIT_KEY_PREFIX)
- [x] `GameMatchService.cs` Lua 스크립트 교체 + TryMatchAsync 3단계 범위 매칭 구현
- [x] `GameMatchService.cs` CancelMatchAsync gameType 파라미터 + wait key DEL 추가
- [x] `GameMatchService.cs` UpdateMatchResultAsync K-factor 계산 + await 전환
- [x] `GameMatchService.cs` UpdateEloRatingsAsync kMultiplier 파라미터 추가
- [x] `GameMatchController.cs` CancelMatch에 [FromQuery] gameType 파라미터 추가
- [x] `GameMatchControllerTests.cs` ELO 통합 테스트 5개 추가 (28→33개)
- [x] `DummyClient` 매칭 재시도 로직 + matchTimeout/avgRatingDiff 리포트 항목 추가
- [x] `dotnet test PlatformA.sln -q` 전체 통과 (246 → 251개)

## 배경
현재 TryMatchAsync는 timestamp를 SortedSet score로 사용하는 선착순(FIFO) 매칭.
ELO 레이팅을 score로 사용하고 TTL 기반 wait key로 대기 시간을 추적하여
3단계(±200→±400→±800→ZPOPMIN)로 범위를 자동 확장한다.
K-factor 감소로 광범위 매칭 시 MMR 희석을 최소화한다.

## 참조
- DB job: `sdlc.ai_jobs.branch = 2026-07-03_AddEloMmrMatching`
- 계획 파일: `~/.claude/plans/7-1-flickering-cook.md`
