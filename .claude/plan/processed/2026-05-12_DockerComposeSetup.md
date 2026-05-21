# Plan: Docker Compose 파일 정리 및 브랜치 이동

## Context
docker-compose.yml 생성/수정 작업을 main 브랜치에서 브랜치 없이 직접 수행했다.
CLAUDE.md 정책 위반 — 모든 파일 변경은 작업 브랜치에서 이루어져야 한다.
현재 main에 uncommitted 변경사항이 있으므로 새 브랜치로 옮겨 커밋한다.

## 현재 상태
- 브랜치: `main`
- 삭제된 파일: `Redis/docker-compose.yml`, `Redis/flush_all_redis_cluster.bat`, `Redis/readme.md`, `Redis/run_redis_cluster.bat`
- 신규 파일(untracked): `PlatformA/docker/` (redis-cluster, mariadb, rabbitmq 세 compose 파일)

## 수행 순서

### 1단계: 새 브랜치 생성
uncommitted 상태이므로 stash 없이 바로 브랜치 생성
```bash
git checkout -b 2026-05-12_DockerComposeSetup
```

### 2단계: 변경사항 커밋
```bash
git add -A
git commit -m "chore: docker-compose 파일 구조 정리 (Redis→docker/, MariaDB·RabbitMQ 추가, restart:always 적용)"
```

### 3단계: 빌드/테스트 생략
docker-compose 파일만 변경 — .NET 코드 변경 없음.

### 4단계: push + PR 생성

### 5단계: SPRINT.md 업데이트 및 CLAUDE.md 정책 오류 수정
- `bright-discovering-bear.md`에 복사하지 않도록 CLAUDE.md 수정
- 피드백 메모리 교정
