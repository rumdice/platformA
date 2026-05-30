# 시스템 아키텍처 개요

## 전체 구성도

```mermaid
graph TB
  subgraph 클라이언트 계층
    C[게임 클라이언트<br/>Web / Mobile / DummyClient]
  end

  subgraph API 계층
    A["Auth API<br/>HTTPS :7001<br/>JWT 인증·갱신"]
    T["Ticketing API<br/>HTTPS :7003<br/>대기열·입장권"]
    M["Matching API<br/>HTTPS :7002<br/>1:1 매칭 엔진"]
    U["Utils API<br/>HTTPS :7004<br/>URL 단축·통계"]
  end

  subgraph 게임 계층
    G["Game Server<br/>TCP :7777<br/>실시간 세션"]
  end

  subgraph 데이터 계층
    R["Redis Cluster<br/>6-node :6371-6376<br/>세션·큐·락"]
    DB["MariaDB :3306<br/>db_WebApp / db_LogApp"]
  end

  C -->|"① 로그인 (REST)"| A
  C -->|"② 대기열 진입 (REST + SignalR)"| T
  C -->|"③ 매칭 요청 (REST + SignalR)"| M
  C -->|"④ 게임 접속 (TCP Binary)"| G

  A -->|"refresh token 저장"| R
  T -->|"queue 관리"| R
  M -->|"match queue + Pub/Sub"| R
  G -->|"login lock + active ticket"| R

  A -->|"플레이어 정보"| DB
  M -->|"매치 기록"| DB

  T -.->|"입장권 발급 시그널"| C
  M -.->|"MatchFound SignalR"| C
```

---

## 서비스별 책임

| 서비스 | 포트 | 주요 책임 | Redis 사용 | DB 사용 |
|--------|------|---------|-----------|--------|
| **Auth API** | 7001 (HTTPS) | JWT 발급·갱신·로그아웃, 신규 유저 자동 등록, Rate Limit | `refresh:{playerId}` | `players`, `player_stats` |
| **Ticketing API** | 7003 (HTTPS) | 대기열 진입·이탈·순위 조회, Ghost 유저 감지(Heartbeat), 입장권 발급 | `{ticket:queue}:global`, `ticket:active:user:{userId}` | — |
| **Matching API** | 7002 (HTTPS) | 매칭 큐 관리, 2인 FIFO 매칭, 타임아웃 처리, SignalR 매칭 알림 | `queue:gamematch:1v1`, `global:room_id`, Pub/Sub | `match_records` |
| **Game Server** | 7777 (TCP) | Binary Protobuf 패킷 처리, 분산 락으로 중복 로그인 방지, GameRoom 브로드캐스트 | `player:login_lock:{playerId}` | — |
| **Utils API** | 7004 (HTTPS) | URL 단축(Snowflake+Base62), IP 지오로케이션, 클릭 통계 | Rate Limit | SQLite (`app.db`) |

---

## 통신 방식

| 통신 | 방식 | 용도 |
|------|------|------|
| 클라이언트 ↔ Auth/Ticketing/Matching/Utils | REST over HTTPS | 일반 API 호출 |
| 클라이언트 ↔ Ticketing | SignalR WebSocket | `QueueActivated` 이벤트 수신 |
| 클라이언트 ↔ Matching | SignalR WebSocket | `MatchFound`, `MatchTimeout` 이벤트 수신 |
| 클라이언트 ↔ Game Server | TCP Raw Socket | Binary Protobuf 패킷 |
| Matching API → Game Server | Redis Pub/Sub | 매칭 성사 이벤트 (`channel:match_success`) |

---

## 핵심 설계 원칙

1. **Redis Cluster 필수** — 단일 Redis 인스턴스 사용 금지 (ADR-001)
2. **Binary 패킷 프로토콜** — Game Server 통신은 Protobuf Envelope (ADR-005)
3. **설정 중앙화** — 모든 상수는 `Consts.cs`에서 환경변수로 관리 (ADR-003, ADR-004)
4. **무상태 JWT 인증** — Game Server는 MySQL 직접 접근 안 함
5. **IDbContextFactory** — EF Core DbContext는 Factory 방식으로만 DI (`DbContext` 직접 주입 금지)
6. **Lua 스크립트 원자성** — Redis 멀티키 연산은 Lua 스크립트로 Race Condition 방지
7. **JobQueue 단일 스레드** — GameRoom의 모든 작업은 순차 처리 (lock 불필요)

---

## 서비스 경계 규칙

각 서비스는 자신의 책임 범위 외 작업을 수행하면 안 된다.

| 서비스 | 금지 사항 |
|--------|---------|
| **Auth API** | 게임 로직, 매칭 로직 처리 |
| **Ticketing API** | 매칭 결과 처리, 게임 룸 생성 |
| **Matching API** | 대기열 관리(Ticketing 영역), 직접 TCP 연결 |
| **Game Server** | HTTP API 호출, MySQL 직접 접근 |
| **Utils API** | 게임/인증/매칭 로직 |

---

## 프로젝트 의존성

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

| 서비스 | 프로토콜 | 포트 |
|--------|---------|------|
| Auth API | HTTPS | 7001 |
| Matching API | HTTPS | 7002 |
| Ticketing API | HTTPS | 7003 |
| Utils API | HTTP | 7004 |
| Game Server | TCP | 7777 |
| Redis 마스터 1~3 | TCP | 6371~6373 |
| Redis 레플리카 1~3 | TCP | 6374~6376 |
| MySQL | TCP | 3306 |

---

## 런타임 버전

<!-- RUNTIME_TABLE -->
| 서비스 | .NET 버전 | 비고 |
|--------|----------|------|
| Auth API | .NET 10.0 |  |
| Ticketing API | .NET 10.0 |  |
| Matching API | .NET 10.0 |  |
| Game Server | .NET 10.0 | Console App (ASP.NET Core 아님) |
| Utils API | .NET 10.0 |  |
<!-- /RUNTIME_TABLE -->
