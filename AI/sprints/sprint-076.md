---
sprint: 76
title: 오목 상용화 개선 4종
branch: 2026-06-29_CommercializeGomoku
date: 2026-06-29
status: done
completed: 2026-06-29
pr: https://github.com/rumdice/platformA/pull/109
---

# Sprint #76 — 오목 상용화 개선 4종

## 목표
게임 결과 저장·MMR/ELO·Rate Limit·헬스체크를 개선하여 오목 게임을 상용화 가능한 수준으로 완성한다.

## 태스크
- [x] 게임 결과 저장 수정: GomokuRoom→Matching.API 파이프라인 디버그 및 재시도 로직 추가
- [x] MMR/ELO 시스템 구현: PlayerRating 엔티티 + ELO 계산 + 게임 종료 시 자동 업데이트
- [x] Rate Limit 재설계: 로그인 임계값 현실화 (100/분), Auth.API Program.cs 적용
- [x] Game.Gomoku 헬스체크 추가: /healthz(liveness JSON) + /readyz(Matching.API 연결 확인)
- [x] DummyClient ServiceManager Gomoku 헬스체크 TCP→HTTP:7779/healthz 전환

## 배경
E2E 결과(sprint-075): loginOk=60/1000(6%), verifyOk=0/7(0%). Rate Limit이 IP 기반이어서 E2E에서 1000명이 ::1을 공유해 10명만 통과. GomokuRoom.ReportMatchResultAsync()는 fire-and-forget으로 실패를 로깅하지 않아 게임 결과가 저장되지 않음. MMR 시스템 미구현. Gomoku 헬스체크 없어 ServiceManager가 TCP 포트로만 확인 중.

## 참조
- DB job: `sdlc.ai_jobs.branch = 2026-06-29_CommercializeGomoku`
