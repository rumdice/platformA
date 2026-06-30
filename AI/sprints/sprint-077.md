---
sprint: 77
title: Rate Limit 유저 기반 전환
branch: 2026-06-30_SwitchUserRateLimit
date: 2026-06-30
status: in-progress
---

# Sprint #77 — Rate Limit 유저 기반 전환

## 목표
Auth.API 로그인 Rate Limit 정책을 IP 기반에서 username(요청 본문) 기반으로 전환하여 E2E 1000명 동시 로그인 시 개인별 제한이 적용되도록 개선한다.

## 태스크
- [ ] `Consts.cs`에 `RATE_LIMIT_USER_KEY_PREFIX` 상수 등록
- [ ] `RedisRateLimitFilter` 또는 Rate Limit 미들웨어를 username 기반으로 수정
- [ ] Auth.API `Program.cs` 정책 연결 확인
- [ ] 테스트 갱신 (username 기반 키 검증)
- [ ] E2E 시나리오 Rate Limit 개선 확인 가능 여부 기록

## 배경
Sprint #76 E2E 결과: 로그인 임계값을 10→100/분으로 높여 통과율을 개선했지만, IP 기반이므로 1000명이 동일 IP(`::1`)를 공유하면 100명 초과 시 전원 차단. username을 Rate Limit 식별자로 사용하면 사용자별 독립적인 제한이 적용되어 E2E 1000명 모두 통과 가능하다.

## 참조
- DB job: `sdlc.ai_jobs.branch = 2026-06-30_SwitchUserRateLimit`
