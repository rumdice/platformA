---
sprint: 83
title: ServiceManager -c Release 플래그 추가
branch: 2026-07-01_FixServiceManagerReleaseBuild
date: 2026-07-01
status: done
completed: 2026-07-01
pr: https://github.com/rumdice/platformA/pull/117
---

# Sprint #83 — ServiceManager -c Release 플래그 추가

## 목표
ServiceManager.Launch()가 `dotnet run --no-build` (Debug 기본값)로 서비스를 기동해 Release 전용 빌드 산출물을 찾지 못해 모든 서비스가 즉시 종료되는 E2E 실패 버그를 수정한다.

## 태스크
- [ ] ServiceManager.cs Launch() 메서드에 `-c Release` 플래그 추가
- [ ] E2E 시나리오 10 재실행으로 서비스 기동 성공 확인

## 배경
/e2e 스킬은 `dotnet build PlatformA.sln -c Release`만 실행하므로 Release 바이너리만 존재한다.
ServiceManager.Launch()는 `dotnet run --no-build`(기본 Debug)로 서비스를 기동하려 하지만
`bin/Debug/net10.0/`에 실행 파일이 없어 서비스가 즉시 종료된다.
결과적으로 E2E 헬스체크가 180초 동안 0/5만 반환한다.

## 참조
- DB job: `sdlc.ai_jobs.branch = 2026-07-01_FixServiceManagerReleaseBuild`
