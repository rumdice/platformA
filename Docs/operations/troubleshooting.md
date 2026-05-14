# 트러블슈팅 가이드

실제 운영 및 개발 환경에서 발생할 수 있는 문제와 해결 방법을 정리합니다.

---

## Redis 클러스터 연결 실패

### 증상
- 서비스 시작 직후 `readyz` 엔드포인트에서 `redis` 상태가 `Unhealthy` 반환
- 로그에 `No connection is available to service this operation` 또는 `It was not possible to connect to the redis server(s)` 오류 출력
- Auth API, Ticketing API, Matching API 모든 서비스가 Redis에 의존하므로 세 서비스 모두 영향을 받음

### 원인
- Redis 클러스터 Docker 컨테이너가 실행되지 않음
- 클러스터 초기화가 완료되기 전에 서비스가 Redis에 연결을 시도함 (타이밍 문제)
- `REDIS_CONNECTION_STRING` 환경변수 값이 잘못 설정됨
- 클러스터 노드 중 과반수 장애로 클러스터 상태가 `fail`로 전환됨

### 해결

**1단계: 컨테이너 실행 여부 확인**

```bash
docker ps | grep redis
```

**2단계: 클러스터 상태 확인**

```bash
docker exec redis-master-1 redis-cli -p 6371 cluster info | grep cluster_state
# 정상: cluster_state:ok
# 장애: cluster_state:fail
```

**3단계: 클러스터가 `fail` 상태인 경우 재구성**

```bash
cd Redis
docker-compose down -v && docker-compose up -d

# 클러스터 초기화 완료 대기 (약 15초)
sleep 15
docker exec redis-master-1 redis-cli -p 6371 cluster info
```

> `docker-compose down -v`는 볼륨을 포함해 삭제하므로 **기존 Redis 데이터가 모두 소실됩니다.** 개발 환경에서만 사용하십시오.

**4단계: 연결 문자열 확인**

`REDIS_CONNECTION_STRING` 환경변수가 설정된 경우 해당 값을 확인합니다. 미설정 시 기본값은 `Consts.cs`에 정의된 값을 사용합니다.

```bash
# 현재 적용 중인 연결 문자열 확인 (기본값)
# PlatformA/PlatformA.Library/Common/Consts.cs
# REDIS_CONNECTION_STRING = "127.0.0.1:6371,127.0.0.1:6372,127.0.0.1:6373"
```

---

## MariaDB 마이그레이션 오류

### 증상
- `dotnet ef database update` 실행 시 오류 발생
- 서비스 시작 시 `Table 'db_WebApp.xxx' doesn't exist` 오류 로그
- `readyz` 엔드포인트에서 `mysql-webapp` 상태가 `Unhealthy` 반환

### 원인 A: DB가 존재하지 않음

```bash
# 오류 예시
# Unknown database 'db_WebApp'
```

**해결**

```bash
mysql -u root -ppass1234 -e "CREATE DATABASE IF NOT EXISTS db_WebApp CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
mysql -u root -ppass1234 -e "CREATE DATABASE IF NOT EXISTS db_LogApp CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"

cd PlatformA/PlatformA.MySqlDB.Lib
dotnet ef database update --context DbWebAppContext
dotnet ef database update --context DbLogAppContext
```

### 원인 B: Migration 히스토리 불일치

```bash
# 오류 예시
# There is already an object named 'xxx' in the database.
# An error occurred while applying the migration 'xxx'.
```

**해결**

```bash
cd PlatformA/PlatformA.MySqlDB.Lib

# Migration 목록 및 현재 적용 상태 확인
dotnet ef migrations list --context DbWebAppContext

# 마지막으로 정상 적용된 Migration으로 롤백
dotnet ef database update <이전Migration이름> --context DbWebAppContext

# 문제 Migration 제거 (로컬 파일 삭제)
dotnet ef migrations remove --context DbWebAppContext

# 재생성 후 적용
dotnet ef migrations add <MigrationName> --context DbWebAppContext --output-dir Migrations/WebApp
dotnet ef database update --context DbWebAppContext
```

### 원인 C: MySQL 서버 접속 불가

**해결**

```bash
# MySQL 서버 실행 여부 확인
mysql -u root -ppass1234 -e "SELECT 1;"

# Docker로 MySQL을 실행 중인 경우
docker ps | grep mysql
docker start <mysql-container-name>
```

---

## JWT 토큰 검증 실패

### 증상
- API 요청에 `Authorization: Bearer <token>` 헤더를 포함했음에도 `401 Unauthorized` 응답
- 로그에 `IDX10223: Lifetime validation failed` 또는 `IDX10214: Audience validation failed` 메시지 출력

### 원인 A: 토큰 만료

Access Token의 기본 만료 시간은 **15분**입니다 (`ACCESS_TOKEN_EXPIRY_MINUTES = 15`).

**해결**

Refresh Token을 사용해 새 Access Token을 발급받습니다.

```bash
# Refresh Token으로 재발급
POST https://localhost:7001/api/Auth/refresh
Content-Type: application/json
{
  "refreshToken": "<refresh_token_value>"
}
```

Refresh Token 만료 기간은 **7일**입니다 (`REFRESH_TOKEN_EXPIRY_DAYS = 7`). 7일이 경과한 경우 재로그인이 필요합니다.

### 원인 B: Issuer / Audience 불일치

Auth API가 발급한 토큰의 Issuer는 `PlatformA.Auth.API`, Audience는 `PlatformA.Services`입니다. 이 값과 다른 토큰을 사용하면 검증에 실패합니다.

**확인 방법**

```bash
# JWT 페이로드를 base64 디코딩해서 확인 (Linux/macOS)
echo "<payload_part>" | base64 -d
```

### 원인 C: `JWT_SECRET` 환경변수 불일치

Auth API와 토큰을 검증하는 다른 서비스가 서로 다른 시크릿 키를 사용하는 경우 검증이 실패합니다.

**확인 방법**

`JWT_SECRET` 환경변수가 모든 서비스에서 동일하게 설정되어 있는지 확인합니다. 환경변수가 없는 경우 `Consts.cs`의 기본값(`YourSuperSecretKeyForPlatformAMSA!@#123`)이 사용됩니다.

```bash
# 환경변수 확인 (PowerShell)
$env:JWT_SECRET

# 환경변수 확인 (bash)
echo $JWT_SECRET
```

---

## 포트 충돌 (7001~7004 범위)

### 증상
- `dotnet run` 실행 시 `System.Net.Sockets.SocketException: Only one usage of each socket address is permitted` 오류
- 서비스가 시작되지 않고 즉시 종료

### 서비스별 기본 포트

| 서비스 | 개발 포트 |
|---|---|
| Auth API | 7001 (HTTPS) |
| Ticketing API | 7003 (HTTPS) |
| Matching API | 7002 (HTTPS) |
| Game Server | 7777 (TCP) |

> `Consts.cs`에 정의된 `AUTH_API_URL`, `TICKET_API_URL`, `MATCH_API_URL` 기본값은 각각 7001, 7003, 7002 포트를 사용합니다. 환경변수로 재정의 가능합니다.

### 원인
- 이전에 실행한 서비스가 종료되지 않고 포트를 점유하고 있음
- 다른 프로그램이 동일 포트를 사용 중

### 해결 (Windows)

```powershell
# 특정 포트를 점유하고 있는 프로세스 확인 (예: 7001 포트)
netstat -ano | findstr :7001

# PID로 프로세스 이름 확인
tasklist /FI "PID eq <PID>"

# 프로세스 강제 종료
taskkill /PID <PID> /F
```

**여러 서비스 일괄 종료:**

```powershell
# dotnet 프로세스 전체 종료 (주의: 다른 dotnet 프로세스도 종료됨)
Get-Process dotnet | Stop-Process -Force
```

### 해결 (Linux/macOS)

```bash
# 포트 점유 프로세스 확인
lsof -i :7001

# 프로세스 강제 종료
kill -9 <PID>
```

---

## DocFX 빌드 오류

### 증상
- `docfx build Docs/docfx.json` 실행 시 오류 발생
- `Docs/_site/` 디렉토리가 생성되지 않거나 일부 페이지가 빠짐

### 원인 A: 소스 프로젝트 빌드 실패

DocFX는 XML 문서 파일(`.xml`)을 참조하기 때문에 소스 프로젝트가 먼저 빌드되어 있어야 합니다.

**해결**

```bash
# 소스 솔루션 먼저 빌드
cd PlatformA
dotnet build PlatformA.sln

# 이후 DocFX 빌드 실행
cd ..
docfx build Docs/docfx.json
```

### 원인 B: DocFX가 설치되지 않음

**해결**

```bash
# DocFX 설치 (.NET Tool)
dotnet tool install -g docfx

# 버전 확인
docfx --version
```

### 원인 C: `toc.yml` 파일 구문 오류

`toc.yml` 파일의 YAML 구문이 잘못된 경우 해당 섹션의 목차가 생성되지 않습니다.

**확인 방법**

```bash
# toc.yml 구문 오류 확인 (python이 설치된 경우)
python -c "import yaml; yaml.safe_load(open('Docs/operations/toc.yml'))"
```

올바른 `toc.yml` 형식 예시:

```yaml
- name: 배포 가이드
  href: deployment.md
- name: 모니터링
  href: monitoring.md
- name: 트러블슈팅
  href: troubleshooting.md
```

### 원인 D: `_site/` 디렉토리 캐시 문제

이전 빌드 결과물이 남아 있어 충돌이 발생하는 경우입니다.

**해결**

```bash
# 빌드 캐시 삭제 후 재빌드
Remove-Item -Recurse -Force Docs/_site, Docs/api, Docs/obj -ErrorAction SilentlyContinue
docfx build Docs/docfx.json
```

### 로컬 서빙 (빌드 결과 미리보기)

```bash
docfx serve Docs/_site
# 기본 포트: http://localhost:8080
```
