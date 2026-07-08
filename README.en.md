# PlatformA

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![License](https://img.shields.io/badge/license-MIT-green)

> [한국어](README.md) | **English**

**High-performance distributed multiplayer game backend built on .NET 10.**

- **ELO-based matchmaking** with 3-stage range expansion (±200 → ±400 → ±800) and K-factor dampening
- **Redis Cluster** (3 master + 3 replica) for distributed state, rate limiting, and atomic Lua operations
- **Dual-protocol game server**: REST/SignalR for lobby & matchmaking, TCP Protobuf for real-time gameplay

---

## Architecture

```mermaid
flowchart LR
    Client(["Client / DummyClient"])

    subgraph API["API Layer (HTTPS)"]
        Auth["Auth API\n:7001"]
        Ticket["Ticketing API\n:7003"]
        Match["Matching API\n:7002"]
        Utils["Utils API\n:7004"]
    end

    subgraph Game["Game Layer"]
        Lobby["Game.Lobby\n:7777 SignalR"]
        Gomoku["Game.Gomoku\n:7778 TCP Protobuf"]
    end

    subgraph Data["Data Layer"]
        Redis[("Redis Cluster\n6371-6376")]
        MySQL[("MySQL / MariaDB\ndb_WebApp · db_LogApp")]
        SQLite[("SQLite\nUtils only")]
    end

    Client -->|JWT| Auth
    Client -->|Queue ticket| Ticket
    Client -->|Match request| Match
    Client -->|WebSocket| Lobby
    Lobby -->|Match found| Gomoku
    Client -->|TCP Binary| Gomoku

    Auth & Ticket & Match --> Redis
    Auth & Match --> MySQL
    Ticket --> Redis
    Utils --> SQLite
```

---

## Services

| Service | Port | Protocol | Role |
|---------|------|----------|------|
| **Auth API** | 7001 | HTTPS REST | JWT issuance & refresh, BCrypt auth, auto-register |
| **Ticketing API** | 7003 | HTTPS REST + SignalR | Queue management (max 10,000), entry tickets |
| **Matching API** | 7002 | HTTPS REST + SignalR | 1v1 ELO matchmaking, match records, result reporting |
| **Game.Lobby** | 7777 | HTTP + SignalR | Lobby hub, match requests, user presence |
| **Game.Gomoku** | 7778 | TCP Binary (Protobuf) | Real-time Gomoku PvP game server |
| **Utils API** | 7004 | HTTPS REST | URL shortening (Snowflake + Base62), click analytics |

---

## Tech Stack

| Category | Technology | Key Packages |
|----------|-----------|-------------|
| **Runtime** | .NET 10.0 (C#) | — |
| **Database** | MariaDB/MySQL (EF Core) · SQLite | `Pomelo.EntityFrameworkCore.MySql` · `Microsoft.EntityFrameworkCore.Sqlite` |
| **Cache / State** | Redis Cluster | `StackExchange.Redis` 2.10.1 · `RedLock.net` 2.3.2 |
| **Communication** | REST · SignalR WebSocket · TCP Binary | `Google.Protobuf` 3.29.3 · `Microsoft.AspNetCore.SignalR` |
| **Auth** | JWT (15 min access + 7 day refresh) · BCrypt | `System.IdentityModel.Tokens.Jwt` 8.16 · `BCrypt.Net-Next` 4.0.3 |
| **Resilience** | Circuit breaker · Retry · Rate limiting | `Polly` 8.4.1 · Redis Lua scripts |
| **I/O** | High-perf buffer management | `System.IO.Pipelines` 10.0 |
| **Logging** | Structured logging | `log4net` 2.0.17 |
| **API Docs** | OpenAPI / Scalar UI | `Microsoft.AspNetCore.OpenApi` · `Scalar.AspNetCore` 2.14 |
| **Infrastructure** | Container · CI | Docker · GitHub Actions |
| **Testing** | Unit & integration tests | `xUnit` · `Moq` · `InMemory DB` — 255 tests / 6 projects |

---

## AI-Assisted Development Workflow

This project is developed using an AI-assisted SDLC pipeline powered by **Claude Code**.
Every feature goes through the following automated stages:

```mermaid
flowchart LR
    P(["/plan\nBranch + DB init"])
    R(["/requirement\nSpec file"])
    I(["/impact\nRisk analysis"])
    S(["/start\nCoding mode"])
    C(["Coding\nLLM implements"])
    T(["/test-gen\nTest generation"])
    D(["/done\nBuild + Test + Push"])
    V(["/review\nCode review"])
    PR(["/pr\nPR creation"])

    P --> R --> I --> S --> C --> T --> D --> V --> PR
```

| Stage | Tool | What happens |
|-------|------|-------------|
| `/plan` | Claude Code | Creates feature branch, registers sprint in SDLC DB |
| `/requirement` | Claude Code | Writes spec file, checks ADR conflicts |
| `/impact` | Claude Code | Classifies risk (HIGH / MEDIUM / LOW), checks references |
| `/start` | Claude Code | Claims job lock, transitions to coding mode |
| Coding | LLM (Claude) | Implements per spec; file-by-file with progress output |
| `/test-gen` | Claude Code + `test-writer` agent | Generates xUnit tests matching existing factory patterns |
| `/done` | Claude Code | Runs `dotnet build`, `dotnet format`, `dotnet test`; pushes only on full pass |
| `/review` | Claude Code | Checks ADR compliance, security, DI, Redis key rules |
| `/pr` | Claude Code + GitHub CLI | Creates PR, archives spec, records cost log |

**Infrastructure behind the workflow:**

```
Claude Code (LLM)  ─── writes code, drives pipeline
       │
       ├── PostgreSQL (SDLC DB)  ─── tracks job/step/gate state
       ├── n8n                   ─── orchestrates GitHub ↔ DB events
       └── GitHub Actions        ─── CI: build, test, lint (read-only; no DB access)
```

> See [`CLAUDE.md`](CLAUDE.md) for the full workflow definition and [`AI/adr/`](AI/adr/) for architectural decisions.

---

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- MySQL / MariaDB running on `localhost:3306` (root password: `pass1234`)

### 1. Start Redis Cluster

```bash
cd PlatformA/docker/redis-cluster
docker-compose up -d
```

### 2. Initialize Databases (first run only)

```bash
mysql -u root -ppass1234 -e "CREATE DATABASE IF NOT EXISTS db_WebApp; CREATE DATABASE IF NOT EXISTS db_LogApp;"
```

### 3. Apply EF Core Migrations

```bash
cd PlatformA/PlatformA.MySqlDB.Lib
dotnet ef database update --context DbWebAppContext
dotnet ef database update --context DbLogAppContext
```

### 4. Run Services

Open a separate terminal for each service:

```bash
cd PlatformA/PlatformA.Auth.API      && dotnet run   # https://localhost:7001
cd PlatformA/PlatformA.Ticketing.API && dotnet run   # https://localhost:7003
cd PlatformA/PlatformA.Matching.API  && dotnet run   # https://localhost:7002
cd PlatformA/PlatformA.Game.Lobby    && dotnet run   # http://localhost:7777
cd PlatformA/PlatformA.Game.Gomoku   && dotnet run   # tcp://localhost:7778
```

> **Note**: Start services in the order above. Auth API must be healthy before Matching API begins matchmaking.

---

## API Documentation

Auto-generated guides are in [`Docs/api-guide/`](Docs/api-guide/):

| Guide | Coverage |
|-------|----------|
| [`auth.md`](Docs/api-guide/auth.md) | Login, token refresh, logout |
| [`ticketing.md`](Docs/api-guide/ticketing.md) | Queue entry, status, entry ticket |
| [`matching.md`](Docs/api-guide/matching.md) | Match request, cancel, result reporting |
| [`utils.md`](Docs/api-guide/utils.md) | URL shorten, redirect, statistics |
| [`game-server-protocol.md`](Docs/api-guide/game-server-protocol.md) | TCP packet format (Protobuf) |

---

## Running Tests

```bash
cd PlatformA
dotnet test PlatformA.sln -q
```

| Test Project | Tests | Focus |
|-------------|-------|-------|
| `Tests.Auth.API` | 24 | JWT, BCrypt, registration |
| `Tests.Ticketing.API` | 21 | Queue logic, rate limits |
| `Tests.Matching.API` | 37 | ELO matching, DB fallback |
| `Tests.Utils.API` | 28 | URL shortening, Base62 |
| `Tests.Game.Gomoku` | 62 | Game logic, win detection |
| `Tests.Game.Lobby` | 64 | SignalR hub, match flow |
| **Total** | **255** | |

---

## Project Structure

```
platformA/
├── PlatformA/
│   ├── PlatformA.sln
│   ├── PlatformA.Library/           # Shared: Redis, JWT, Consts, TCP base
│   ├── PlatformA.Library.Game/      # Game infra: GameSession, GameRoom, JobQueue
│   ├── PlatformA.MySqlDB.Lib/       # EF Core contexts & migrations
│   ├── PlatformA.Auth.API/          # Authentication service
│   ├── PlatformA.Ticketing.API/     # Queue management service
│   ├── PlatformA.Matching.API/      # Matchmaking service
│   ├── PlatformA.Game.Lobby/        # Lobby hub (SignalR)
│   ├── PlatformA.Game.Gomoku/       # Gomoku game server (TCP)
│   ├── PlatformA.Utils.API/         # URL shortening service
│   ├── PlatformA.Game.DummyClient/  # E2E test client
│   └── PlatformA.Tests.*/           # Test projects (6)
├── AI/
│   ├── ARCHITECTURE.md              # System design overview
│   ├── adr/                         # Architectural Decision Records (11)
│   ├── sprints/                     # Sprint history
│   └── workreport/                  # Daily work reports
└── Docs/
    ├── api-guide/                   # API documentation
    └── architecture/                # Sequence diagrams, DB schema
```

---

## Contributing

All changes go through a branch workflow: create a feature branch → implement → build & test → pull request into `main`.  
See [`CLAUDE.md`](CLAUDE.md) for the full AI-assisted SDLC pipeline used in this project.
