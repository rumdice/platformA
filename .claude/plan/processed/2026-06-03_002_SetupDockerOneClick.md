# 요구사항 명세: SetupDockerOneClick

작성일: 2026-06-03
브랜치: 2026-06-03_SetupDockerOneClick (PR #64, 머지 완료)
소스: task JSON summary (소급 작성 — /requirement 누락으로 사후 기록)

## 요구사항 요약

외부 사용자가 `git clone` 후 setup 스크립트 1회 실행 + `docker compose up` 만으로
전체 게임 서비스 스택(Redis·MariaDB·Auth/Matching/Ticketing/Utils API·Game.Server)이
정상 동작하도록 Docker 환경을 원클릭화한다.

## 상세 요구사항

1. **MariaDB DB 자동 생성**: 컨테이너 최초 기동 시 `db_WebApp`·`db_LogApp` 자동 생성
   - `docker/mariadb/init/01-create-databases.sql` — MariaDB `initdb.d` 메커니즘 활용
2. **EF Core 마이그레이션 자동화**: `db-migrator` 서비스 추가
   - `PlatformA.MySqlDB.Lib/Dockerfile.migrator` — SDK 이미지 기반 일회성 컨테이너
   - MariaDB healthy 후 기동, 완료 후 exit 0
   - Auth.API·Matching.API는 `db-migrator: service_completed_successfully` 의존
3. **개발 인증서 자동화**: `docker/setup.sh` + `docker/setup.ps1`
   - `dotnet dev-certs https --export-path ./certs/devcert.pfx` 자동 실행
   - `.env.example` → `.env` 복사 포함
4. **환경변수 파라미터화**: `docker-compose.full.yml`의 하드코딩 값 → `${VAR:-default}` 패턴
5. **Utils.API SQLite 영구 볼륨**: `utils-api-sqlite` named volume + `/data/app.db` 마운트
6. **DummyClient profile**: `--profile testing` 으로 선택적 기동

## 영향 범위 (예상)

| 파일 | 유형 | 위험도 |
|------|------|--------|
| `docker/docker-compose.full.yml` | 수정 | 🟢 LOW |
| `docker/mariadb/init/01-create-databases.sql` | 신규 | 🟢 LOW |
| `PlatformA.MySqlDB.Lib/Dockerfile.migrator` | 신규 | 🟢 LOW |
| `docker/setup.sh` / `setup.ps1` | 신규 | 🟢 LOW |
| `docker/.env.example` | 신규 | 🟢 LOW |

## 제약 및 주의사항

- ADR-004 준수: 설정값 환경변수화, `${VAR:-default}` 패턴으로 `.env` 없이도 동작
- `initdb.d` SQL은 볼륨이 비어있을 때만 실행 — 기존 데이터 무영향
- `dotnet dev-certs` 는 호스트 신뢰 저장소 등록 필요 — compose 내부 실행 불가, setup 스크립트 필수
- `db-migrator`는 EF Core 디자인타임 도구 필요 → SDK 이미지 사용 (런타임 이미지 아님)

## 구현 접근 방향

- MariaDB: `initdb.d` → EF Migration → API 서비스 순 의존성 체인
- `setup.sh`/`setup.ps1`: 인증서 존재 시 덮어쓰지 않음, `.env` 존재 시 덮어쓰지 않음 (재실행 안전)
- `WEBAPP_DB_CONNECTION`/`LOGAPP_DB_CONNECTION` — `DbWebAppContextFactory`/`DbLogAppContextFactory` 가 읽는 환경변수명 준수

## 검증 기준

- `.\docker\setup.ps1` 실행 후 `devcert.pfx`와 `.env` 자동 생성 확인
- `docker compose up -d --build` 성공, `docker logs db-migrator` 에서 "All migrations applied" 확인
- `https://localhost:7001/healthz` 200 OK
- `https://localhost:7002/healthz` / `7003/healthz` / `7004/healthz` 200 OK
