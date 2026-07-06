---
sprint: 86
title: GetPlayerRatingAsync DB Fallback
branch: 2026-07-06_RatingDbFallback
date: 2026-07-06
status: done
completed: 2026-07-06
pr: https://github.com/rumdice/platformA/pull/120
---

# Sprint #86 — GetPlayerRatingAsync DB Fallback

## 목표
Redis miss 시 기본값 1000 대신 DB PlayerRatings 테이블을 조회하여 실제 ELO 레이팅을 반환한다.
DB hit 시 Redis에 1시간 TTL로 재캐싱한다. ELO/MMR 매칭 신뢰도 안정화.

## 태스크
- [x] `GameMatchService.GetPlayerRatingAsync` DB fallback 추가 (Redis hit → 즉시 반환, Redis miss → DB 조회 → 재캐싱 → 반환, DB miss → 1000)
- [x] 테스트 4개 추가 (Redis hit, Redis miss+DB hit, DB fallback 후 재캐싱 확인, DB miss 기본값)
- [x] `dotnet test PlatformA.sln -q` 전체 통과

## 배경
현재 GetPlayerRatingAsync는 Redis miss 시 DB를 조회하지 않고 DEFAULT_PLAYER_RATING(1000)을 반환한다.
Redis 캐시 만료 후 실제 레이팅이 높은 유저도 1000점으로 매칭되어 ELO 매칭 품질이 저하된다.

## 참조
- DB job: `sdlc.ai_jobs.branch = 2026-07-06_RatingDbFallback`
- 계획 파일: `.claude/plan/2026-07-06_001_RatingDbFallback.md`
