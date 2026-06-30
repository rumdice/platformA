# 요구사항 명세: FixE2EScenario9TcpHealth

작성일: 2026-07-01
브랜치: 2026-07-01_FixE2EScenario9TcpHealth
소스: task JSON summary + workreport 2026-06-30 개선 항목

## 요구사항 요약
/e2e 스킬 시나리오 9에서 Game.Gomoku 헬스체크 방식을 `http://localhost:7779/healthz`(HTTP) 에서
`Test-NetConnection -Port 7778`(TCP) 방식으로 변경하여 Windows 비관리자 권한 환경에서 발생하는
오탐(항상 실패로 판정)을 제거한다.

## 상세 요구사항

1. **헬스체크 로직 수정** (3단계, 136번째 라인 부근)
   - `G=$(check_http "http://localhost:7779/healthz")` →
     `G=$(check_tcp "localhost" 7778)` 으로 변경
   - 준비 완료 판정 조건: `[ "$G" != "000" ]` → `[ "$G" = "OK" ]` 으로 변경
     (check_tcp는 "OK" / "FAIL"을 반환하므로 기존 HTTP 상태코드 비교와 다름)
   - 로그 출력에서 `Gomoku=$G` 표기는 그대로 유지

2. **강제 종료 포트 수정** (3단계, 112번째 라인 부근)
   - `kill_port 7779` → `kill_port 7778` 로 변경
     (Gomoku의 실제 TCP 리스닝 포트는 7778)

3. **서비스 종료 단계 포트 수정** (5단계, 200번째 라인 부근)
   - `for PORT in 7001 7002 7003 7777 7779` → `for PORT in 7001 7002 7003 7777 7778`

## 영향 범위 (예상)
- `.claude/skills/e2e/SKILL.md` — 1개 파일, 3곳 수정

## 제약 및 주의사항
- `check_tcp` 함수는 이미 SKILL.md 내에 정의되어 있음 (127~129번째 라인) — 신규 함수 추가 불필요
- READY 카운터 증가 조건(5개 서비스)은 그대로 유지
- 다른 서비스(Auth, Ticket, Match, Lobby)는 HTTP 헬스체크를 사용하며 변경 없음
- ServiceManager(시나리오 10)는 PR #113에서 이미 수정됨 — 이번 변경 대상 아님

## 구현 접근 방향
SKILL.md의 3곳을 정밀 수정한다:
1. 136번째 라인: check_http → check_tcp 전환 + 반환값 비교 방식 변경
2. 112번째 라인: kill_port 포트 번호 7779 → 7778
3. 200번째 라인: for 루프 포트 목록 7779 → 7778

## 검증 기준
- `/e2e 9` 실행 시 Gomoku 헬스체크 대기 로그에서 `Gomoku=OK`가 출력됨
- 시나리오 9가 120초 내 5/5 서비스 준비 완료를 인식하고 DummyClient 실행에 진입함
- 전체 빌드·테스트 통과 (SKILL.md는 C# 코드 아님 → dotnet test 영향 없음)
