---
sprint: 90
title: Session Disconnect 안전성 수정
branch: 2026-07-14_FixSessionDisconnectSafety
date: 2026-07-14
status: in-progress
---

# Sprint #90 — Session Disconnect 안전성 수정

## 목표
`Session.Disconnect()`에서 `_socket.RemoteEndPoint` 접근 시 `SocketException(107)` 발생으로 `OnDisconnected()`가 건너뛰어지는 버그를 수정하여 Redis 로그인 락 미해제(24시간 유저 접속 불가) 및 고스트 세션 문제를 제거한다.

## 태스크
- [x] Session.cs Disconnect() RemoteEndPoint try-catch 분리
- [x] Session.cs Disconnect() Shutdown/Close try-catch 추가
- [x] 세션 종료 관련 유닛 테스트 추가/보완
- [x] 빌드·테스트 통과 확인

## 배경
ProjectA.Operation 장애 분석 문서(TROUBLESHOOTING_Operation_Crash.md) 검토 중 PlatformA의 `Session.cs:60`에서 동일한 패턴이 발견됨.  
`_socket.RemoteEndPoint`는 `Socket.Connected == true`이더라도 RST 패킷 수신 후 OS 내부 상태 갱신 타이밍에 따라 `SocketException(107) ENOTCONN`을 던질 수 있다. 이 경우 `OnDisconnected()`가 호출되지 않아:
- `ReleaseLockAsync()` 미실행 → 유저 로그인 락 86400초(24시간) 동안 해제 불가
- `room.Push(() => room.Leave(this))` 미실행 → 게임 방 고스트 세션

ProjectA는 Thread Pool 콜백 방식으로 프로세스 크래시까지 발생했으나,
PlatformA는 async Task 패턴 (.NET 8)으로 크래시는 없지만 조용히 리소스 누수가 쌓인다.

## 참조
- DB job: `sdlc.ai_jobs.branch = 2026-07-14_FixSessionDisconnectSafety`
