# 시스템 아키텍처

> 이 문서의 설계 원칙은 ADR 없이 변경 불가합니다.
> 변경이 필요하면 `AI/adr/` 에 새 ADR을 작성하고 사용자 승인을 받으십시오.

---

## 전체 구조

```
클라이언트 (Web/Mobile/DummyClient)
        │
        ▼
 [Load Balancer / Nginx]
        │
        ├──────────────────────────────────────────────┐
        ▼                                              ▼
[Auth API :7088]                              [Utils API :포트]
 - 로그인/로그아웃                             - URL 단축
 - JWT 발급 (Access 15분, Refresh 7일)        - IP 조회
 - BCrypt 비밀번호 검증                        - 클릭 통계
        │
        ▼ (JWT 발급 후 클라이언트가 직접 호출)
        │
        ├────────────────────────────────────────┐
        ▼                                        ▼
[Ticketing API :7075]                  [Matching API :5189]
 - 대기열 진입/이탈/상태조회             - 매칭 요청 접수
 - SignalR Hub (/hubs/queue)            - 매칭 엔진 (백그라운드)
 - Rate Limit: 5req/s                   - SignalR Hub (/hubs/matching)
 - Ghost 유저 감지/정리                  - Redis Pub/Sub 발행
        │                                        │
        └──────────────┬─────────────────────────┘
                       │
                       ▼
               [Redis Cluster]
          3 Master + 3 Replica
          포트: 6371~6376
                       │
                       │ (Pub/Sub: match_success_channel)
                       ▼
              [Game Server :7777]
               - TCP 소켓 (raw)
               - System.IO.Pipelines
               - Binary 패킷 프로토콜
               - 게임룸 관리

                [MySQL :3306]
          db_WebApp | db_LogApp
```

---

## 서비스별 책임 경계

### Auth API
- **소유 데이터**: Player 엔티티 (MySQL db_WebApp)
- **책임**: 인증 토큰 생명주기 전체
- **금지**: 게임 로직, 매칭 로직 처리

### Ticketing API
- **소유 데이터**: Redis 대기열 (`{ticket:queue}:global`), 하트비트
- **책임**: 대기열 순서 관리, 입장권(Active 상태) 발급
- **금지**: 매칭 결과 처리, 게임 룸 생성

### Matching API
- **소유 데이터**: Redis 매칭 큐
- **책임**: 2인 매칭 성사, Redis Pub/Sub으로 Game Server에 알림
- **금지**: 대기열 관리 (Ticketing 영역), 직접 TCP 연결

### Game Server
- **소유 데이터**: 인메모리 게임 룸 상태
- **책임**: 실제 게임 플레이 처리, 패킷 파싱/직렬화
- **금지**: HTTP API 호출, MySQL 직접 접근

### Utils API
- **소유 데이터**: SQLite (app.db) - ShortUrl 테이블
- **책임**: 독립적인 유틸리티 기능
- **금지**: 게임/인증/매칭 로직

---

## 서비스 간 통신 방식

| 발신 | 수신 | 방식 | 용도 |
|------|------|------|------|
| 클라이언트 | Auth API | HTTPS REST | 로그인/토큰 갱신 |
| 클라이언트 | Ticketing API | HTTPS REST + SignalR | 대기열 진입, 실시간 알림 |
| 클라이언트 | Matching API | HTTP REST + SignalR | 매칭 요청, 결과 수신 |
| Matching API | Game Server | Redis Pub/Sub | 매칭 성공 이벤트 |
| 클라이언트 | Game Server | TCP (Binary) | 실제 게임플레이 |

**원칙**: 서비스 간 직접 HTTP 호출 최소화. 비동기 이벤트(Redis Pub/Sub) 우선.

---

## 데이터 저장소 역할

### Redis Cluster (6371~6376)
- 세션/토큰 저장 (Refresh Token)
- 대기열 ZSet (`{ticket:queue}:global`)
- 하트비트 ZSet (`{ticket:queue}:heartbeats`)
- Active 사용자 Hash (`ticket:active:user:{id}`)
- 매칭 큐
- Rate Limiting ZSet (슬라이딩 윈도우)
- URL 캐시 (Utils: `url:{code}`, `stats:{code}`)
- Pub/Sub 채널 (`match_success_channel`)

### MySQL db_WebApp (포트 3306)
- `players` — 유저 계정 (Auth API 소유)
- `player_stats` — 플레이어 통계
- `match_records` — 매칭 이력 (Matching API 소유)

### MySQL db_LogApp
- `access_logs` — 접근 로그

### SQLite app.db (Utils API 로컬)
- `short_urls` — URL 단축 데이터

---

## 핵심 설계 원칙 (변경 불가)

1. **Redis Cluster 필수**: 단일 Redis 인스턴스 사용 금지 (ADR-001)
2. **Binary 패킷 프로토콜**: Game Server 통신은 JSON 사용 금지 (ADR-002)
3. **Protobuf 기반 패킷 직렬화**: `packets.proto` 정의 → `Grpc.Tools` 빌드 타임 자동 생성. 수동 직렬화 코드 작성 금지 (ADR-007)
4. **설정 중앙화**: 모든 상수는 `Consts.cs` 에서만 관리
5. **JWT 무상태 인증**: 게임 서버는 MySQL 직접 접근 안 함
6. **IDbContextFactory**: EF Core DbContext는 Factory 방식으로만 DI

---

## 프로젝트 의존성 그래프

```
PlatformA.Library
        │ (참조)
        ├── PlatformA.Auth.API
        ├── PlatformA.Matching.API
        ├── PlatformA.Ticketing.API
        ├── PlatformA.Utils.API
        └── PlatformA.Game.Server

PlatformA.MySqlDB.Lib
        │ (참조)
        ├── PlatformA.Auth.API
        └── PlatformA.Matching.API
```

---

## 포트 맵

| 서비스 | 프로토콜 | 포트 | 비고 |
|--------|---------|------|------|
| Auth API | HTTPS | 7001 | run-scenarios 기준 |
| Matching API | HTTPS | 7002 | run-scenarios 기준 |
| Ticketing API | HTTPS | 7003 | run-scenarios 기준 |
| Utils API | HTTP | 7004 | run-scenarios 기준 |
| Game Server | TCP | 7777 | Binary 패킷 |
| Redis 마스터 1 | TCP | 6371 | |
| Redis 마스터 2 | TCP | 6372 | |
| Redis 마스터 3 | TCP | 6373 | |
| Redis 레플리카 1~3 | TCP | 6374~6376 | |
| MySQL | TCP | 3306 | |
