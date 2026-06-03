# 요구사항 명세: RestructureDockerSdlcInfra

작성일: 2026-06-03
브랜치: 2026-06-03_RestructureDockerSdlcInfra
소스: plan mode (~/.claude/plans/n8n-docker-cheeky-allen.md)

## 요구사항 요약

`docker/sdlc/` 하위에 통합되어 있던 PostgreSQL·n8n을 각각 독립 폴더(`docker/postgresql/`, `docker/n8n/`)로 분리한다.
각 폴더는 docker-compose.yml 파일 하나만 가지며, 독립 실행이 가능하다.
전체 스택 통합 실행은 기존 `docker/docker-compose.full.yml`에 두 서비스를 추가하는 방식으로 일원화한다.

## 상세 요구사항

1. **`docker/postgresql/docker-compose.yml` 생성**
   - 독립 실행용 PostgreSQL 16
   - 기본값 하드코딩 (별도 .env 파일 없음)
   - 네트워크: `postgresql-net`, 볼륨: `sdlc-postgres-data`
   - healthcheck 포함

2. **`docker/n8n/docker-compose.yml` 생성**
   - 독립 실행용 n8n (SQLite 백엔드 — postgres 의존성 없음)
   - 기본값 하드코딩 (별도 .env 파일 없음)
   - 네트워크: `n8n-net`, 볼륨: `sdlc-n8n-data`

3. **`docker/docker-compose.full.yml` 수정**
   - `sdlc-postgres-data`, `sdlc-n8n-data` 볼륨 추가
   - `postgres` 서비스 추가: PostgreSQL 16, platformA-net, healthcheck 포함
   - `n8n` 서비스 추가: postgres 백엔드 사용, `depends_on: postgres (healthy)`, platformA-net
   - 환경변수는 `${VAR:-default}` 형식으로 docker/.env 재활용

4. **`docker/.env.example` 수정**
   - PostgreSQL 섹션 추가: `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`
   - n8n 섹션 추가: `N8N_BASIC_AUTH_USER`, `N8N_BASIC_AUTH_PASSWORD`, `N8N_HOST`

5. **`docker/sdlc/` 폴더 전체 삭제**
   - `.env`, `.env.example`, `setup-sdlc.ps1`, `setup-sdlc.sh`, `docker-compose.yml` 포함

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|------|----------|
| `docker/postgresql/docker-compose.yml` | 신규 생성 |
| `docker/n8n/docker-compose.yml` | 신규 생성 |
| `docker/docker-compose.full.yml` | 수정 (서비스·볼륨 추가) |
| `docker/.env.example` | 수정 (섹션 추가) |
| `docker/sdlc/` | 삭제 |

C# 소스 코드 변경 없음 — Docker 인프라 구성 파일만 영향.

## 제약 및 주의사항

- 개별 compose 파일에 별도 `.env` 파일 생성 금지 (기본값은 파일 내 하드코딩)
- 별도 setup 스크립트 추가 금지 — 기존 `docker/setup.ps1`, `docker/setup.sh`가 `docker/.env` 생성을 담당
- full compose에서 n8n은 postgres가 healthy 상태일 때만 시작 (`condition: service_healthy`)
- full compose의 독립 n8n(SQLite)과 full compose의 n8n(postgres) 데이터 비호환 — 전환 시 볼륨 초기화 필요
- 포트 충돌 주의: PostgreSQL 5432, n8n 5678

## 구현 접근 방향

- 각 compose 파일은 자기완결적으로 기본값 내장 → 사용자가 별도 설정 없이 `docker compose up -d`로 실행 가능
- full compose는 `${VAR:-default}` 패턴으로 `docker/.env` 값 우선 사용, 없으면 기본값 fallback
- full compose에서 postgres와 n8n은 기존 `platformA-net`을 공유하여 게임 서비스와 같은 네트워크에 위치

## 검증 기준

- [ ] `docker/postgresql/docker-compose.yml up -d` → postgres 컨테이너 healthy 상태 확인
- [ ] `docker/n8n/docker-compose.yml up -d` → n8n 컨테이너 기동, `http://localhost:5678` 접근 가능
- [ ] `docker/docker-compose.full.yml up -d` → 전체 스택 기동 시 postgres·n8n 포함 확인
- [ ] `docker/sdlc/` 폴더 미존재 확인
- [ ] `docker/.env.example`에 PostgreSQL·n8n 섹션 존재 확인
