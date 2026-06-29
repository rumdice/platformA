# 요구사항 명세: CommercializeGomoku

작성일: 2026-06-29
브랜치: 2026-06-29_CommercializeGomoku
소스: sprint-076.md + 사용자 직접 입력 (E2E 결과 분석 기반)

## 요구사항 요약
E2E 시나리오 10 결과(loginOk=6%, verifyOk=0%)에서 도출된 4개 버그/결함을 수정하여
오목 게임 백엔드가 실제 상용화가 가능한 수준이 되도록 개선한다.

## 상세 요구사항

### 1. 게임 결과 저장 수정 (Stage 10 verifyOk=0 버그)
- **증상**: GomokuRoom이 게임 종료 후 Matching.API /api/gamematch/result를 호출하지만 verifyOk=0
- **원인**: `ReportMatchResultAsync()`가 fire-and-forget이며 예외를 Console.WriteLine만 처리
- **요구사항**:
  - Matching.API에서 `/api/gamematch/result` 엔드포인트 동작 먼저 직접 확인 (curl/로그)
  - GomokuRoom이 Matching.API를 호출할 때 SSL 인증서 검증 우회 (개발환경 자체서명 인증서)
  - 실패 시 최대 3회 재시도 (지수 백오프: 1s, 2s, 4s)
  - 실패 로그를 Console.Error가 아닌 구조화된 형태로 남김
  - 타임아웃 5초 설정 (기존 무제한)

### 2. MMR/ELO 시스템 구현
- **현황**: match_records 테이블에 WinnerId 있으나 ELO 업데이트 없음
- **요구사항**:
  - `player_ratings` 테이블 신규: PlayerId(FK), Rating(float, 기본 1000), WinCount, LoseCount, DrawCount, UpdatedAt
  - ELO 계산: K-factor 32 (신규), 16 (300판 이상). 기대승률 표준 ELO 공식 사용
  - 게임 종료 시(GameMatchService.SaveMatchResultAsync) 양 플레이어 ELO 자동 업데이트
  - GET /api/gamematch/rating/{userId} 엔드포인트 추가 (인증 필요)
  - EF Core Migration 생성 필수

### 3. Rate Limit 재설계 (IP→유저 기반, 임계값 현실화)
- **현황**: `rl:login:{clientIp}` — 1000명 E2E가 ::1 공유 → 10명만 통과
- **요구사항**:
  - 로그인 엔드포인트: IP 기반 유지하되 임계값 상향 (permitLimit: 10 → 100, window: 60s)
    - 이유: 로그인은 userId를 아직 모르므로 IP 기반이 적합; 100/min은 실서비스 기준 충분
  - 인증 필요 엔드포인트(티켓·매칭 등): 유저 기반 Rate Limit 추가
    - 키: `rl:{policyName}:{userId}` (JWT에서 추출)
    - `[RedisRateLimitUser("policy")]` 새 어트리뷰트 또는 기존 어트리뷰트 키 추출 방식 변경
  - Consts.cs에 Rate Limit 관련 상수 정리

### 4. Game.Gomoku 헬스체크 엔드포인트 추가
- **현황**: Game.Gomoku는 TCP 서버만 운영, HTTP 없음. ServiceManager가 TCP:7778 직접 확인
- **요구사항**:
  - `/healthz` (liveness): HttpListener로 HTTP GET 응답 200 OK `{"status":"alive"}`
    - TCP 서버가 열려 있는지만 확인 — 외부 의존성 체크 없음
  - `/readyz` (readiness): Matching.API HTTP GET /healthz 호출 성공 여부 확인
    - 실패 시 503 반환
  - 포트: 7779 (TCP 게임 서버 7778과 분리)
  - HttpListener 사용 (관리자 권한 필요시 netsh 명령으로 URL 예약 또는 localhost만 허용)
  - ServiceManager 헬스체크를 TCP:7778 → HTTP:7779/healthz 로 변경

## 영향 범위 (예상)

| 파일 | 변경 내용 |
|------|---------|
| `PlatformA.Game.Gomoku/Rooms/GomokuRoom.cs` | ReportMatchResultAsync 재시도 로직 |
| `PlatformA.Game.Gomoku/Server/GameServer.cs` | HttpListener 시작/중지 추가 |
| `PlatformA.Game.Gomoku/Program.cs` | HttpListener 포트 7779 설정 |
| `PlatformA.Matching.API/Services/GameMatchService.cs` | ELO 업데이트 로직 추가 |
| `PlatformA.Matching.API/Controllers/GameMatchController.cs` | GET /rating/{userId} 엔드포인트 추가 |
| `PlatformA.MySqlDB.Lib/DBWebApp/Entities/PlayerRating.cs` | 신규 엔티티 |
| `PlatformA.MySqlDB.Lib/DBWebApp/DbWebAppContext.cs` | PlayerRatings DbSet 등록 |
| `PlatformA.MySqlDB.Lib/Migrations/WebApp/` | AddPlayerRatings 마이그레이션 |
| `PlatformA.Auth.API/Program.cs` | login Rate Limit permitLimit 10→100 |
| `PlatformA.Library/RateLimit/RedisRateLimiterService.cs` | 유저 기반 Rate Limit 지원 |
| `PlatformA.Library/RateLimit/RedisRateLimitAttribute.cs` | userId 추출 로직 (선택) |
| `PlatformA.Library/Common/Consts.cs` | Rate Limit 관련 상수 |
| `PlatformA.Game.DummyClient/ServiceManager.cs` | Gomoku 헬스체크 URL 변경 |
| `PlatformA.Game.DummyClient/Program.cs` | FindRepoRoot 버그 수정 (미커밋) |

## 제약 및 주의사항

- **ADR-001 Redis Cluster**: Rate Limit 키는 Consts.cs 상수 사용 필수
- **ADR-007 Protobuf**: Game.Gomoku 내 패킷 변경 없음, TCP 서버 로직 불변
- **EF Core Migration**: `db-migrator` 에이전트 없이도 가능하나 Up()/Down() 반드시 확인
- **Game.Gomoku HttpListener**: Windows 비관리자에서 `http://+:7779/` 예약 필요.
  `http://localhost:7779/` 로 제한하면 예약 불필요 → 로컬 환경에서는 localhost만 사용
- **Consts.cs 관리**: 새 Redis 키, Rate Limit 정책명은 모두 Consts.cs에 상수 등록

## 구현 접근 방향

1. **게임 결과 저장 수정** 먼저: GomokuRoom에서 HttpClient에 SSL bypass 추가 + 재시도 3회.
   Matching.API 엔드포인트 로그 확인 후 GomokuRoom 코드 수정.

2. **MMR/ELO**: PlayerRating 엔티티 → DbContext 등록 → Migration 생성 → 
   GameMatchService에 ELO 계산 메서드 추가 → SaveMatchResultAsync에서 호출 → 
   컨트롤러에 rating 엔드포인트 추가.

3. **Rate Limit**: Auth.API Program.cs에서 login 정책 permitLimit 변경 (가장 단순).
   인증 엔드포인트 유저 기반: RedisRateLimiterService에 `ExecuteByUserAsync` 메서드 추가,
   새 어트리뷰트 `[RedisRateLimitUser]` 작성. Ticketing.API·Matching.API에 적용.

4. **Game.Gomoku 헬스체크**: Program.cs에서 HttpListener 스레드 시작,
   /healthz → 200, /readyz → Matching.API ping 후 200/503.
   ServiceManager.cs에서 TcpPort:7778 → HealthUrl:"http://localhost:7779/healthz" 변경.

5. **DummyClient 미커밋 수정**: FindRepoRoot + --no-build 변경을 이 브랜치에서 커밋.

## 검증 기준

- `dotnet build PlatformA.sln` 오류 0
- `dotnet test PlatformA.sln` 전체 통과
- E2E 시나리오 10 재실행 시:
  - loginOk ≥ 850/1000 (Rate Limit 개선으로 90% 이상 기대)
  - verifyOk = 게임완주 수와 동일 (100% — 저장 실패 없어야 함)
- Matching.API /api/gamematch/rating/{userId} 응답 확인
- Game.Gomoku 기동 후 http://localhost:7779/healthz → 200 응답 확인
