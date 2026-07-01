---
sprint: 82
title: Task.Run 예외 무음 개선
branch: 2026-07-01_FixTaskRunSilentException
date: 2026-07-01
status: done
completed: 2026-07-01
pr: https://github.com/rumdice/platformA/pull/116
---

# Sprint #82 — Task.Run 예외 무음 개선

## 목표
Game.Gomoku에서 `_ = Task.Run(...)` 패턴 내부 예외가 로그 없이 사라지는 문제를 수정하여 운영 중 장애 원인 파악을 가능하게 한다.

## 태스크
- [x] Program.cs HTTP 헬스체크 루프 예외 로깅 추가
- [x] GomokuRoom.cs 턴 타임아웃 루프 예외 로깅 추가
- [x] 빌드·테스트 통과 확인

## 배경
`_ = Task.Run(async () => { ... })` 패턴은 fire-and-forget이므로 내부에서 예외가 발생해도
호출자에게 전파되지 않는다. 현재 Game.Gomoku에는 이 패턴이 두 곳 사용되는데
예외 발생 시 아무런 로그가 남지 않아 운영 장애 진단이 불가능하다.
ILogger를 통해 LogError로 기록하여 가시성을 확보한다.

## 참조
- DB job: `sdlc.ai_jobs.branch = 2026-07-01_FixTaskRunSilentException`
