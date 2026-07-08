# PlatformA

[![GitHub](https://img.shields.io/badge/GitHub-rumdice%2FplatformA-181717?logo=github)](https://github.com/rumdice/platformA)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![License](https://img.shields.io/badge/license-MIT-green)

> **한국어** | [English](README.en.md)

**.NET 10 기반 고성능 분산 멀티플레이어 게임 백엔드 플랫폼.**

- **ELO 기반 매칭**: 3단계 범위 확장(±200 → ±400 → ±800) + K-factor 감소로 MMR 희석 방지
- **Redis Cluster** (Master 3 + Replica 3): 분산 상태 관리, Rate Limiting, Lua 스크립트 원자성 보장
- **이중 프로토콜 게임 서버**: 로비·매칭은 REST/SignalR, 실시간 게임플레이는 TCP Protobuf

---

## 아키텍처

```mermaid
flowchart TB
    Client(["클라이언트"])

    Auth["Auth API\n로그인 · 토큰 발급"]
    Ticket["Ticketing API\n대기열 관리"]
    Lobby["Game.Lobby\n로비 허브"]
    Match["Matching API\n매칭 · 전적 기록"]
    Gomoku["Game.Gomoku\n게임 서버"]
    Utils["Utils API\nURL 단축"]

    subgraph DB["데이터 저장소"]
        direction LR
        Redis[("Redis\n캐시 · 매칭 상태")]
        MySQL[("MySQL\n플레이어 · 전적")]
        SQLite[("SQLite\nUtils 전용")]
    end

    Client -->|"① 로그인"| Auth
    Client -->|"② 대기열 입장"| Ticket
    Client -->|"③ 로비 접속"| Lobby
    Lobby  -->|"④ 매칭 신청"| Match
    Match  -->|"⑤ 매칭 완료"| Lobby
    Lobby  -->|"⑥ 게임 서버 안내"| Client
    Client -->|"⑦ 게임 접속"| Gomoku
    Gomoku -->|"⑧ 결과 기록"| Match

    Auth   -.-> Redis & MySQL
    Ticket -.-> Redis
    Match  -.-> Redis & MySQL
    Gomoku -.-> Redis
    Utils  -.-> SQLite
```

### 주요 사용자 흐름

```text
인증
→ 입장 대기열
→ Lobby 접속
→ Matching API 매칭 요청
→ MatchFound 알림
→ 게임 서버 접속 정보 수신
→ Gomoku TCP 서버 접속
→ 게임 진행
→ 게임 결과 보고
```

---

## 서비스 목록

| 서비스 | 포트 | 프로토콜 | 역할 |
|--------|------|---------|------|
| **Auth API** | 7001 | HTTPS REST | JWT 발급·갱신·로그아웃, BCrypt 인증, 신규 유저 자동 등록 |
| **Ticketing API** | 7003 | HTTPS REST + SignalR | 대기열 관리, 입장권 발급 |
| **Matching API** | 7002 | HTTPS REST + SignalR | 1:1 ELO 매칭, 매치 기록, 게임 시작·결과 보고 |
| **Game.Lobby** | 7777 | HTTP + SignalR | 로비 허브, 매칭 요청 중계, 유저 프레젠스, MatchFound 전달 |
| **Game.Gomoku** | 7778 | TCP Binary (Protobuf) | 실시간 오목 PvP 게임 서버 |
| **Utils API** | 7004 | HTTPS REST | URL 단축(Snowflake + Base62), 클릭 통계 |

---

## 기술 스택

| 분류 | 기술 | 주요 패키지 |
|------|------|-----------|
| **런타임** | .NET 10.0 (C#) | — |
| **데이터베이스** | MariaDB/MySQL (EF Core) · SQLite | `Pomelo.EntityFrameworkCore.MySql` · `Microsoft.EntityFrameworkCore.Sqlite` |
| **캐시 / 상태** | Redis Cluster | `StackExchange.Redis` 2.10.1 · `RedLock.net` 2.3.2 |
| **통신** | REST · SignalR WebSocket · TCP Binary | `Google.Protobuf` 3.29.3 · ASP.NET Core SignalR |
| **인증** | JWT (Access 15분 + Refresh 7일) · BCrypt | `System.IdentityModel.Tokens.Jwt` 8.16 · `BCrypt.Net-Next` 4.0.3 |
| **내결함성** | 서킷 브레이커 · 재시도 · Rate Limiting | `Polly` 8.4.1 · Redis Lua 스크립트 |
| **I/O** | 고성능 버퍼 관리 | `System.IO.Pipelines` 10.0 |
| **로깅** | 구조적 로깅 | `log4net` 2.0.17 |
| **API 문서** | OpenAPI / Scalar UI | `Microsoft.AspNetCore.OpenApi` · `Scalar.AspNetCore` 2.14 |
| **인프라** | 컨테이너 · CI | Docker · GitHub Actions |
| **테스트** | 단위·통합 테스트 | `xUnit` · `Moq` · InMemory DB — 6개 테스트 프로젝트 |

> 테스트 개수는 기능 추가에 따라 자주 변하므로 README에 고정하지 않는다. 최신 결과는 `dotnet test` 출력과 CI 결과를 기준으로 확인한다.

---

## AI 기반 개발 워크플로우

이 프로젝트는 **Claude Code**를 활용한 AI 지원 SDLC 파이프라인으로 개발된다.  
모든 기능은 아래의 자동화된 단계를 거쳐 PR로 완성된다.

```mermaid
flowchart LR
    P(["/plan\n브랜치 + DB 초기화"])
    R(["/requirement\n요구사항 명세"])
    I(["/impact\n위험도 분석"])
    S(["/start\n코딩 모드"])
    C(["코딩\n개발자 + LLM 협업"])
    T(["/test-gen\n테스트 자동 생성"])
    D(["/done\n빌드+테스트+Push"])
    V(["/review\n코드 리뷰"])
    PR(["/pr\nPR 생성"])

    P --> R --> I --> S --> C --> T --> D --> V --> PR
```

| 단계 | 도구 | 수행 내용 |
|------|------|---------|
| `/plan` | Claude Code | 기능 브랜치 생성, SDLC DB에 스프린트 등록 |
| `/requirement` | Claude Code | 명세 파일 작성, ADR 충돌 검사 |
| `/impact` | Claude Code | 위험도 분류(HIGH / MEDIUM / LOW), 참조 관계 확인 |
| `/start` | Claude Code | Job Lock 획득, 코딩 모드 전환 |
| 코딩 | 개발자 + LLM | 개발자가 설계의 방향·경계를 결정하고 LLM이 구현을 보조 |
| `/test-gen` | Claude Code + `test-writer` 에이전트 | 기존 팩토리 패턴을 재사용하는 xUnit 테스트 생성 |
| `/done` | Claude Code | `dotnet build` → `dotnet format` → `dotnet test` 통과 시 Push |
| `/review` | 개발자 + Claude Code | 실행 흐름, ADR, 보안, DI, Redis 키, 동시성 검토 |
| `/pr` | Claude Code + GitHub CLI | PR 생성, 명세 파일 archived, 비용 로그 기록 |

### 워크플로우 인프라

```text
Claude Code (LLM)  ─── 코드 작성 보조, 파이프라인 구동
       │
       ├── PostgreSQL (SDLC DB)  ─── job/step/gate 상태 추적
       ├── n8n                   ─── GitHub ↔ DB 이벤트 오케스트레이션
       └── GitHub Actions        ─── CI: 빌드·테스트·린트
```

### AI 워크플로우 기술 스택

| 구분 | 도구 | 버전 / 비고 |
|------|------|-----------|
| LLM 에이전트 | Claude Code | claude-sonnet-4-6 |
| SDLC 상태 DB | PostgreSQL | `platforma_sdlc` 스키마, `sdlc.ai_jobs` 테이블 |
| 오케스트레이션 | n8n | GitHub ↔ DB 이벤트 브리지 |
| CI/CD | GitHub Actions | 빌드·테스트·린트 전용 |
| PR 관리 | GitHub CLI (`gh`) | PR 생성·조회·레이블 |
| DB 접근 스크립트 | Python + psycopg2 | `.github/scripts/db_write.py` 등 로컬 전용 |
| 스프린트 추적 | Markdown (`AI/sprints/`) | `sprint-NNN.md` YAML 프론트매터 |
| 아키텍처 결정 | ADR (`AI/adr/`) | 아키텍처 의사결정 기록 |

> 전체 워크플로우 정의는 [`CLAUDE.md`](CLAUDE.md), 아키텍처 결정 이력은 [`AI/adr/`](AI/adr/) 참조.

---

## 빠른 시작

### 사전 요구사항

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- MySQL / MariaDB (`localhost:3306`)
- 로컬 환경에 맞는 DB 계정과 비밀번호
- EF Core CLI가 없다면 `dotnet tool install --global dotnet-ef`

> 실제 비밀번호와 연결 문자열은 소스 코드나 공개 문서에 하드코딩하지 말고 User Secrets, 환경변수 또는 로컬 전용 설정으로 관리한다.

### 1. Redis 클러스터 시작

```bash
cd PlatformA/docker/redis-cluster
docker compose up -d
```

### 2. 데이터베이스 초기화 (최초 1회)

`-p` 옵션 다음에 비밀번호를 직접 적지 않으면 CLI가 안전하게 비밀번호 입력을 요청한다.

```bash
mysql -u root -p -e "CREATE DATABASE IF NOT EXISTS db_WebApp; CREATE DATABASE IF NOT EXISTS db_LogApp;"
```

### 3. EF Core 마이그레이션 적용

```bash
cd PlatformA/PlatformA.MySqlDB.Lib
dotnet ef database update --context DbWebAppContext
dotnet ef database update --context DbLogAppContext
```

### 4. 서비스 실행

각 서비스를 별도 터미널에서 실행한다.

```bash
cd PlatformA/PlatformA.Auth.API      && dotnet run   # https://localhost:7001
cd PlatformA/PlatformA.Ticketing.API && dotnet run   # https://localhost:7003
cd PlatformA/PlatformA.Matching.API  && dotnet run   # https://localhost:7002
cd PlatformA/PlatformA.Game.Lobby    && dotnet run   # http://localhost:7777
cd PlatformA/PlatformA.Game.Gomoku   && dotnet run   # tcp://localhost:7778
```

> **권장 실행 순서:** 전체 사용자 흐름을 검증할 때는 Auth → Ticketing → Matching → Lobby → Gomoku 순서로 실행한다.  
> Matching API의 직접적인 준비 상태 의존성은 Redis와 MySQL이며, Auth API는 Matching API의 기동 자체를 위한 직접 의존성이 아니다.

### 5. 상태 확인

지원하는 서비스는 아래 엔드포인트로 상태를 확인한다.

```text
/healthz  → 프로세스 생존 여부
/readyz   → Redis, DB 등 외부 의존성을 포함한 트래픽 수용 가능 여부
```

---

## API 문서

자동 생성 API 가이드는 [`Docs/api-guide/`](Docs/api-guide/)에 있다.

| 가이드 | 내용 |
|--------|------|
| [`auth.md`](Docs/api-guide/auth.md) | 로그인, 토큰 갱신, 로그아웃 |
| [`ticketing.md`](Docs/api-guide/ticketing.md) | 대기열 진입, 상태 조회, 입장권 |
| [`matching.md`](Docs/api-guide/matching.md) | 매칭 요청, 취소, 결과 보고 |
| [`utils.md`](Docs/api-guide/utils.md) | URL 단축, 리다이렉트, 통계 |
| [`game-server-protocol.md`](Docs/api-guide/game-server-protocol.md) | TCP 패킷 포맷(Protobuf) |

---

## 테스트 실행

```bash
cd PlatformA
dotnet test PlatformA.sln -q
```

| 테스트 프로젝트 | 주요 대상 |
|----------------|----------|
| `PlatformA.Tests.Auth.API` | JWT, BCrypt, 회원가입 |
| `PlatformA.Tests.Ticketing.API` | 대기열 로직, Rate Limit |
| `PlatformA.Tests.Matching.API` | ELO 매칭, DB 폴백 |
| `PlatformA.Tests.Utils.API` | URL 단축, Base62 |
| `PlatformA.Tests.Game.Gomoku` | 게임 로직, 승리 판정 |
| `PlatformA.Tests.Game.Lobby` | SignalR 허브, 매칭 흐름 |

테스트 개수를 문서에 반영해야 할 경우에도 각 프로젝트의 수치를 임의로 합산하지 말고 실제 `dotnet test` 또는 CI 결과를 기준으로 갱신한다.

---

## 프로젝트 구조

```text
platformA/
├── README.md
├── README.en.md
├── PlatformA/
│   ├── PlatformA.sln
│   ├── PlatformA.Library/           # Redis, JWT, TCP, Packet 등 공통 기반
│   ├── PlatformA.Library.Game/      # GameSession, GameRoom 등 게임 공통 기반
│   ├── PlatformA.MySqlDB.Lib/       # EF Core 컨텍스트와 마이그레이션
│   ├── PlatformA.SdlcDB.Lib/        # SDLC PostgreSQL 데이터 계층
│   ├── PlatformA.Auth.API/          # 인증 서비스
│   ├── PlatformA.Ticketing.API/     # 대기열 관리 서비스
│   ├── PlatformA.Matching.API/      # 매칭 서비스
│   ├── PlatformA.Game.Lobby/        # 로비 허브(SignalR)
│   ├── PlatformA.Game.Gomoku/       # 오목 게임 서버(TCP)
│   ├── PlatformA.Utils.API/         # URL 단축 서비스
│   ├── PlatformA.Game.DummyClient/  # E2E 테스트 클라이언트
│   └── PlatformA.Tests.*/           # 테스트 프로젝트
├── AI/
│   ├── ARCHITECTURE.md              # 시스템 설계 개요
│   ├── adr/                         # 아키텍처 결정 기록
│   ├── sprints/                     # 스프린트 이력
│   └── workreport/                  # 일일 작업 리포트
└── Docs/
    ├── api-guide/                   # API 문서
    ├── architecture/                # 시퀀스 다이어그램, DB 스키마
    └── developer-guide/             # 게임 서버·패킷·개발 가이드
```

---

## 문서 유지관리 원칙

- 루트 `README.md`를 프로젝트 개요의 기준 문서로 사용한다.
- `README.en.md`는 한국어 README의 구조와 핵심 사실을 동일하게 유지한다.
- `PlatformA/README.md`가 별도로 존재한다면 아루의 로컬 실행 가이드로 한정하고, Java/Spring 등 실제 프로젝트와 무관한 템플릿 내용은 제거한다.
- 포트, 패키지 버전, 테스트 개수처럼 변경 가능성이 높은 값은 코드 또는 CI 결과와 함께 검증한다.
- Mermaid의 서비스 연결은 실제 호출 방향과 일치하도록 유지한다.

---

## 기여 가이드

모든 변경은 브랜치 워크플로우를 통해 진행한다.

```text
기능 브랜치 생성
→ 요구사항과 영향 범위 기록
→ 구현
→ 빌드·테스트
→ 코드 리뷰
→ main 대상 Pull Request
```

전체 AI 지원 SDLC 파이프라인은 [`CLAUDE.md`](CLAUDE.md) 참조.
