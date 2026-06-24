---
sprint: 73
title: Utils.API 헬스체크 추가
branch: 2026-06-25_AddUtilsHealthcheck
date: 2026-06-25
status: in-progress
---

# Sprint #73 — Utils.API 헬스체크 추가

## 목표

Utils.API에 `/healthz`(liveness)와 `/readyz`(Redis readiness) 엔드포인트를 추가하여
다른 API 서비스들과 동일한 운영 준비 상태를 달성한다.

## 태스크

- [x] Program.cs에 `AddHealthChecks()` 등록 (Redis readiness 포함)
- [x] `/healthz` liveness 엔드포인트 추가 (`Predicate = _ => false`)
- [x] `/readyz` readiness 엔드포인트 추가 (Redis 체크, 503 가능)
- [x] `WriteJsonResponse` 헬퍼 추가 (JSON 형식 응답)
- [x] 빌드/테스트 통과 확인

## 배경

workreport 완성도 평가에서 Utils.API만 헬스체크가 없어 완성도 83%로 평가됨.
Auth.API, Ticketing.API, Matching.API는 모두 `/healthz`+`/readyz` 구현 완료.
운영 환경 로드밸런서 및 k8s probe 호환성을 위해 필요.

## 참조

- DB job: `sdlc.ai_jobs.branch = 2026-06-25_AddUtilsHealthcheck`
- 패턴 참조: `.claude/rules/patterns.md` 섹션 8 (Health Check)
