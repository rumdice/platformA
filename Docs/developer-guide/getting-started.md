# 로컬 환경 세팅

## 사전 요구사항

| 도구 | 버전 | 용도 |
|------|------|------|
| .NET SDK | 10.0+ | 서비스 빌드 |
| Docker Desktop | 최신 | Redis 클러스터, MariaDB |
| Git | 최신 | 소스 관리 |

## 저장소 클론

```bash
git clone https://github.com/rumdice/platformA.git
cd platformA
```

## 인프라 시작

```powershell
# Redis 클러스터 (6-node)
cd PlatformA/docker/redis-cluster
docker-compose up -d

# MariaDB
cd PlatformA/docker/mariadb
docker-compose up -d
```

## 빌드 및 실행

```powershell
# 전체 솔루션 빌드
cd PlatformA
dotnet build PlatformA.sln

# 테스트
dotnet test PlatformA.sln
```

각 서비스는 Visual Studio Code의 `.vscode/launch.json`에 등록된 디버그 구성으로 실행하거나, 개별 `dotnet run` 명령을 사용한다.

| 서비스 | 포트 |
|--------|------|
| Auth API | HTTPS 7001 |
| Matching API | HTTPS 7002 |
| Ticketing API | HTTPS 7003 |
| Utils API | HTTPS 7004 |
| Game Server | TCP 7777 |
