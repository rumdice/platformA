# 요구사항 명세: AddSdlcDockerInfra

작성일: 2026-06-03
브랜치: 2026-06-03_AddSdlcDockerInfra
소스: task JSON summary (소급 작성 — /requirement 누락으로 사후 기록)

## 요구사항 요약

Phase 3 AI_SDLC 인프라로 PostgreSQL 16과 n8n을 기존 `docker/redis-cluster/`·`docker/mariadb/` 와 동일한 폴더 패턴으로 Docker Compose를 통해 로컬 설치 가능하게 한다.

## 상세 요구사항

1. `docker/sdlc/docker-compose.yml` 생성 — PostgreSQL 16 + n8n 서비스 정의
2. `docker/sdlc/.env.example` 생성 — SDLC 인프라 환경변수 템플릿 (패스워드, 포트 등)
3. `docker/sdlc/setup-sdlc.sh` (Linux/macOS) + `setup-sdlc.ps1` (Windows) — 원터치 기동 스크립트
4. PostgreSQL은 `platforma_sdlc` DB 전용, 게임 서비스 MariaDB와 독립
5. n8n 내부 메타데이터는 PostgreSQL의 `n8n` 스키마로 분리 저장
6. `.env` 파일은 `.gitignore`에 이미 전역 등록됨 — 별도 처리 불필요
7. 기존 게임 서비스 compose(`docker-compose.full.yml`)와 **완전 독립** 운영

## 영향 범위 (예상)

| 파일 | 유형 | 위험도 |
|------|------|--------|
| `docker/sdlc/docker-compose.yml` | 신규 | 🟢 LOW |
| `docker/sdlc/.env.example` | 신규 | 🟢 LOW |
| `docker/sdlc/setup-sdlc.sh` | 신규 | 🟢 LOW |
| `docker/sdlc/setup-sdlc.ps1` | 신규 | 🟢 LOW |

C# 소스, Migrations, 테스트 코드 변경 없음.

## 제약 및 주의사항

- ADR-004 준수: 설정값은 환경변수(`${VAR:-default}` 패턴) 사용, 하드코딩 금지
- 게임 서비스 인프라(Redis, MariaDB)와 네트워크 분리(`sdlc-net` 독립 브릿지)
- PostgreSQL 포트 기본 5432 — 다른 PostgreSQL 인스턴스와 충돌 시 `.env`에서 변경
- n8n과 PostgreSQL이 같은 DB 인스턴스를 사용하므로 `platforma` DB 사용자는 두 스키마 모두 접근 가능해야 함

## 구현 접근 방향

- `docker/redis-cluster/`·`docker/mariadb/` 패턴 동일하게 적용
- `setup-sdlc.sh`/`setup-sdlc.ps1`은 `.env.example` → `.env` 복사 + `docker compose up -d` 실행
- PostgreSQL healthcheck: `pg_isready` 명령 사용
- n8n은 PostgreSQL healthy 조건 충족 후 기동 (`depends_on: service_healthy`)

## 검증 기준

- `docker compose -f docker/sdlc/docker-compose.yml up -d` 성공
- `docker ps` 에서 `platforma-sdlc-postgres` (healthy) + `platforma-n8n` 확인
- `http://localhost:5678` 접속 — n8n UI 로그인 화면 표시
- `psql -h localhost -p 5432 -U platforma -d platforma_sdlc` 연결 성공
