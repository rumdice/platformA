---
sprint: 77
title: Rate Limit 유저 기반 전환
branch: 2026-06-30_SwitchUserRateLimit
date: 2026-06-30
status: done
completed: 2026-06-30
pr: https://github.com/rumdice/platformA/pull/110
---

# Sprint #77 — Rate Limit 유저 기반 전환

## 목표
Auth.API 로그인 Rate Limit 정책을 IP 기반에서 username(요청 본문) 기반으로 전환하여 E2E 1000명 동시 로그인 시 개인별 제한이 적용되도록 개선한다.

## 태스크
- [x] `Consts.cs`에 `RATE_LIMIT_LOGIN_PREFIX` 상수 등록 (`rl:login:`)
- [x] `AuthController.Login`에서 `[RedisRateLimit]` 어트리뷰트 제거 → `IsAllowedAsync("login", request.Username)` 직접 호출
- [x] `RedisRateLimiterService.IsAllowedAsync` 파라미터명 `clientIp` → `identifier` 변경
- [x] 테스트 갱신 (`Login_UserRateLimitExceeded_ForUsername_Returns429` 추가, 24개 통과)
- [x] E2E 개선 효과: 동일 IP(`::1`) 공유 시에도 username별 독립 Rate Limit 카운터 적용 → 1000명 E2E 전원 통과 가능

## 배경
Sprint #76 E2E 결과: 로그인 임계값을 10→100/분으로 높여 통과율을 개선했지만, IP 기반이므로 1000명이 동일 IP(`::1`)를 공유하면 100명 초과 시 전원 차단. username을 Rate Limit 식별자로 사용하면 사용자별 독립적인 제한이 적용되어 E2E 1000명 모두 통과 가능하다.

## 참조
- DB job: `sdlc.ai_jobs.branch = 2026-06-30_SwitchUserRateLimit`
