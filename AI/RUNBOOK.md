# RUNBOOK — 빌드 / 실행 / 배포 명령어

> 모든 명령어는 복사-붙여넣기로 바로 실행 가능해야 합니다.
> 새 명령어 발견 시 이 파일에 추가하십시오.

---

## 전제 조건

- .NET SDK 8.0+ 설치
- Docker & Docker Compose 설치
- MySQL 8.0 실행 중 (로컬 또는 Docker)
- Git 브랜치: `claude/analyze-project-structure-oWGle`

---

## 1. 빌드

```bash
# 전체 솔루션 빌드
cd /home/user/platformA/PlatformA
dotnet build PlatformA.sln

# 특정 프로젝트만 빌드
dotnet build PlatformA.Auth.API/PlatformA.Auth.API.csproj
dotnet build PlatformA.Matching.API/PlatformA.Matching.API.csproj
dotnet build PlatformA.Ticketing.API/PlatformA.Ticketing.API.csproj
dotnet build PlatformA.Game.Server/PlatformA.Game.Server.csproj

# Release 빌드
dotnet build PlatformA.sln -c Release
```

---

## 2. 로컬 실행 순서

**반드시 이 순서로 시작할 것** (의존성 순서):

```bash
# Step 1: Redis 클러스터 시작
cd /home/user/platformA/Redis
docker-compose up -d
# 클러스터 초기화 완료 확인 (약 10초 대기)
sleep 10
docker exec -it redis-master-1 redis-cli -p 6371 cluster nodes

# Step 2: MySQL 실행 확인
mysql -u root -ppass1234 -e "SHOW DATABASES;"

# Step 3: DB 마이그레이션 적용 (최초 또는 새 마이그레이션 있을 때)
cd /home/user/platformA/PlatformA/PlatformA.MySqlDB.Lib
dotnet ef database update --context DbWebAppContext

# Step 4: Auth API 실행 (포트 7088)
cd /home/user/platformA/PlatformA/PlatformA.Auth.API
dotnet run

# Step 5: Ticketing API 실행 (포트 7075)
cd /home/user/platformA/PlatformA/PlatformA.Ticketing.API
dotnet run

# Step 6: Matching API 실행 (포트 5189)
cd /home/user/platformA/PlatformA/PlatformA.Matching.API
dotnet run

# Step 7: Game Server 실행 (포트 7777, TCP)
cd /home/user/platformA/PlatformA/PlatformA.Game.Server
dotnet run

# Step 8 (선택): Utils API 실행
cd /home/user/platformA/PlatformA/PlatformA.Utils.API
dotnet run
```

---

## 3. 헬스체크 확인

```bash
# Auth API liveness
curl -f http://localhost:7088/healthz

# Auth API readiness (Redis + MySQL 확인)
curl -f http://localhost:7088/readyz

# Ticketing API readiness
curl -f http://localhost:7075/readyz

# Matching API readiness
curl -f http://localhost:5189/readyz
```

---

## 4. DB 마이그레이션

```bash
cd /home/user/platformA/PlatformA/PlatformA.MySqlDB.Lib

# 새 Migration 생성 (WebApp)
dotnet ef migrations add <MigrationName> \
  --context DbWebAppContext \
  --output-dir Migrations/WebApp

# Migration 적용 (WebApp)
dotnet ef database update --context DbWebAppContext

# 새 Migration 생성 (LogApp)
dotnet ef migrations add <MigrationName> \
  --context DbLogAppContext \
  --output-dir Migrations/LogApp

# Migration 적용 (LogApp)
dotnet ef database update --context DbLogAppContext

# Migration 목록 확인
dotnet ef migrations list --context DbWebAppContext

# 마지막 Migration 롤백
dotnet ef database update <이전Migration이름> --context DbWebAppContext
```

---

## 5. Redis 관리

```bash
# Redis 클러스터 시작
cd /home/user/platformA/Redis
docker-compose up -d

# Redis 클러스터 재시작 (기존 데이터 삭제)
docker-compose down -v && docker-compose up -d

# 클러스터 상태 확인
docker exec -it redis-master-1 redis-cli -p 6371 cluster nodes
docker exec -it redis-master-1 redis-cli -p 6371 cluster info

# 전체 데이터 삭제 (마스터 3개)
for port in 6371 6372 6373; do
  redis-cli -p $port FLUSHALL
done

# 특정 키 확인
redis-cli -p 6371 KEYS "*ticket*"
redis-cli -p 6371 ZRANGE "{ticket:queue}:global" 0 -1 WITHSCORES
```

---

## 6. Docker 빌드

```bash
cd /home/user/platformA/PlatformA

# Auth API
docker build -f PlatformA.Auth.API/Dockerfile -t platformA-auth:latest .

# Ticketing API
docker build -f PlatformA.Ticketing.API/Dockerfile -t platformA-ticketing:latest .

# Game Server
docker build -f PlatformA.Game.Server/Dockerfile -t platformA-game:latest .

# Utils API
docker build -f PlatformA.Utils.API/Dockerfile -t platformA-utils:latest .
```

---

## 7. DummyClient 시나리오 실행

```bash
cd /home/user/platformA/PlatformA/PlatformA.Game.DummyClient
dotnet run
```

인터랙티브 메뉴에서 시나리오 선택:
- `1`: Game Server TCP 연결 테스트
- `2`: Utils API 프론트 페이지 테스트
- `3`: 단일 유저 로그인 → 매칭 테스트 (2개 인스턴스 필요)
- `4`: 1,000명 로그인 + 대기열 처리량 테스트
- `7`: 단일 유저 통합 로그인/재로그인 테스트
- `8`: 중복 로그인 방지 테스트

---

## 8. Git 워크플로우

```bash
# 현재 브랜치 확인
git branch

# 개발 브랜치로 이동 (항상 이 브랜치에서 작업)
git checkout claude/analyze-project-structure-oWGle

# 변경사항 커밋
git add <파일>
git commit -m "feat: 설명"

# 원격에 푸시
git push -u origin claude/analyze-project-structure-oWGle
```

---

## 9. 트러블슈팅

### Redis 연결 실패
```bash
# 클러스터 상태 확인
redis-cli -p 6371 ping
redis-cli -p 6371 cluster info | grep cluster_state

# 클러스터 재구성 (데이터 소실 주의)
cd /home/user/platformA/Redis
docker-compose down -v && docker-compose up -d && sleep 15
```

### DB 마이그레이션 충돌
```bash
# Migration 히스토리 확인
dotnet ef migrations list --context DbWebAppContext

# 특정 시점으로 롤백 후 재적용
dotnet ef database update <이전Migration> --context DbWebAppContext
dotnet ef migrations remove --context DbWebAppContext
```

### 빌드 오류: Source Generator
```bash
# Generator 프로젝트 클린 빌드
cd /home/user/platformA/PlatformA
dotnet clean PlatformA.Generator.Lib/PlatformA.Generator.Lib.csproj
dotnet build PlatformA.sln
```
