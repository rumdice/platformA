# ENVIRONMENT — 환경 설정 가이드

---

## 필수 서비스

| 서비스 | 버전 | 포트 | 역할 |
|--------|------|------|------|
| .NET SDK | 8.0+ | — | 빌드/실행 |
| MySQL | 8.0 | 3306 | 영구 데이터 저장 |
| Redis | 7.x (Cluster) | 6371~6376 | 캐시, 큐, Pub/Sub |
| Docker | 20.x+ | — | Redis 클러스터 실행 |

---

## 모든 설정값 위치

**단일 파일**: `PlatformA/PlatformA.Library/Common/Consts.cs`

```csharp
// JWT
SECRET_KEY          = "YourSuperSecretKeyForPlatformAMSA!@#123"
JWT_ISSUER          = "PlatformA.Auth.API"
JWT_AUDIENCE        = "PlatformA.Services"
ACCESS_TOKEN_EXPIRY_MINUTES = 15
REFRESH_TOKEN_EXPIRY_DAYS   = 7

// Redis
REDIS_CONNECTION_STRING = "127.0.0.1:6371,127.0.0.1:6372,127.0.0.1:6373"

// MySQL
MYSQL_WEBAPP_CONNECTION = "Server=localhost;Port=3306;Database=db_WebApp;User=root;Password=pass1234"
MYSQL_LOGAPP_CONNECTION = "Server=localhost;Port=3306;Database=db_LogApp;User=root;Password=pass1234"

// Game Server
GAME_SERVER_IP   = "127.0.0.1"
GAME_SERVER_PORT = 7777

// Service URLs (클라이언트용 — run-scenarios 기준 포트)
AUTH_API_URL    = "https://localhost:7001/api/Auth/login"
TICKET_API_URL  = "https://localhost:7003"
MATCH_API_URL   = "https://localhost:7002/api/GameMatch/RequestMatch"
MATCH_HUB_URL   = "https://localhost:7002/hubs/matching"

// Queue
WAIT_QUEUE_MAX_SIZE    = 10000
ACTIVE_USER_TTL_SECONDS = 300   // 5분
```

> 설정 변경은 반드시 `Consts.cs`에서만 — ADR-003 참조

---

## 서비스 포트 맵

| 서비스 | 포트 | 비고 |
|--------|------|------|
| Auth API | 7001 (HTTPS) | run-scenarios 기준 |
| Matching API | 7002 (HTTPS) | run-scenarios 기준 |
| Ticketing API | 7003 (HTTPS) | run-scenarios 기준 |
| Utils API | 7004 (HTTP) | run-scenarios 기준 |
| Game Server | 7777 (TCP) | Binary 패킷 |

---

## MySQL 초기 설정

```sql
-- 데이터베이스 생성
CREATE DATABASE db_WebApp CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE db_LogApp CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- 사용자 확인 (기본값: root / pass1234)
SELECT user, host FROM mysql.user;
```

```bash
# EF Core Migration으로 테이블 자동 생성
cd PlatformA/PlatformA.MySqlDB.Lib
dotnet ef database update --context DbWebAppContext
dotnet ef database update --context DbLogAppContext
```

---

## Redis Cluster 초기 설정

```bash
cd PlatformA/docker/redis-cluster

# 최초 실행 (클러스터 자동 구성됨)
docker-compose up -d

# 클러스터 구성 확인 (약 15초 후)
docker exec redis-master-1 redis-cli -p 6371 cluster info

# 예상 출력:
# cluster_state:ok
# cluster_slots_assigned:16384
# cluster_known_nodes:6
```

---

## 개발 환경 빠른 시작

```bash
# 1. 저장소 클론 후 main 브랜치 체크아웃
git checkout main && git pull

# 2. Redis 시작
cd PlatformA/docker/redis-cluster && docker-compose up -d

# 3. MySQL 접속 확인 후 DB 생성
mysql -u root -ppass1234 -e "CREATE DATABASE IF NOT EXISTS db_WebApp; CREATE DATABASE IF NOT EXISTS db_LogApp;"

# 4. Migration 적용
cd PlatformA/PlatformA.MySqlDB.Lib
dotnet ef database update --context DbWebAppContext
dotnet ef database update --context DbLogAppContext

# 5. 솔루션 빌드 확인
cd PlatformA
dotnet build PlatformA.sln
```

---

## 알려진 환경 문제

### Matching API에 Dockerfile 없음
- Matching API(`PlatformA.Matching.API`)는 Docker 이미지 없음
- 로컬에서만 `dotnet run`으로 실행 가능

### 설정값 하드코딩
- 모든 시크릿이 Consts.cs에 평문 저장 (ADR-003)
- 개발 환경 전용. 프로덕션 배포 전 환경변수로 이전 필요

### CORS 설정 (Utils API)
- `AllowAnyOrigin()` 설정 — 개발 전용
- 프로덕션에서는 구체적인 도메인으로 제한 필요
