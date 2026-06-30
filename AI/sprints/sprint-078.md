---
sprint: 78
title: Ticketing.API 테스트 보강
branch: 2026-06-30_EnhanceTicketingTests
date: 2026-06-30
status: in-progress
---

# Sprint #78 — Ticketing.API 테스트 보강

## 목표
Ticketing.API 통합 테스트를 13개에서 20개 이상으로 보강하여 대기열 만료·중복·초과·미존재 등 미커버 경로를 검증한다.

## 태스크
- [ ] 대기열 만료(TTL 초과) 케이스 테스트 추가
- [ ] 중복 티켓 요청 차단 테스트 추가
- [ ] 대기열 최대 사이즈 초과 테스트 추가
- [ ] 티켓 없는 상태에서 대기열 상태 조회 테스트 추가
- [ ] 비정상 토큰/입력값 경계 케이스 테스트 추가

## 배경
현재 13개 테스트는 정상 경로(EnterQueue, LeaveQueue, GetStatus)의 기본 케이스만 커버. E2E 1000명 시나리오에서 비정상 시나리오 대응 신뢰성을 높이기 위해 미커버 경로를 추가한다.

## 참조
- DB job: `sdlc.ai_jobs.branch = 2026-06-30_EnhanceTicketingTests`
