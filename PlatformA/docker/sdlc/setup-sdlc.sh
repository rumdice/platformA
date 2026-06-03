#!/usr/bin/env bash
# PlatformA AI_SDLC Phase 3 인프라 원터치 시작 스크립트 (Linux / macOS)
# 실행: chmod +x docker/sdlc/setup-sdlc.sh && ./docker/sdlc/setup-sdlc.sh
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env"
ENV_EXAMPLE="$SCRIPT_DIR/.env.example"

echo "[SDLC Setup] PlatformA AI_SDLC Phase 3 인프라를 초기화합니다."

# 1. .env 파일 생성 (없는 경우에만)
if [ ! -f "$ENV_FILE" ]; then
  if [ -f "$ENV_EXAMPLE" ]; then
    cp "$ENV_EXAMPLE" "$ENV_FILE"
    echo "[SDLC Setup] .env 파일 생성 완료. 필요 시 수정하세요: $ENV_FILE"
  else
    echo "[Error] .env.example 파일을 찾을 수 없습니다: $ENV_EXAMPLE"
    exit 1
  fi
else
  echo "[SDLC Setup] .env 파일이 이미 존재합니다: $ENV_FILE"
fi

# 2. Docker Compose 기동
echo "[SDLC Setup] PostgreSQL + n8n 컨테이너를 시작합니다..."
docker compose -f "$SCRIPT_DIR/docker-compose.yml" up -d

# 3. 상태 확인
echo ""
echo "[SDLC Setup] 컨테이너 상태:"
docker compose -f "$SCRIPT_DIR/docker-compose.yml" ps

echo ""
echo "[SDLC Setup] 완료!"
echo ""
echo "  PostgreSQL: localhost:5432"
echo "    psql -h localhost -p 5432 -U platforma -d platforma_sdlc"
echo ""
echo "  n8n UI: http://localhost:5678"
echo "    기본 계정: admin / admin_dev_password (.env에서 변경 가능)"
echo ""
echo "  종료:   docker compose -f docker/sdlc/docker-compose.yml down"
echo "  초기화: docker compose -f docker/sdlc/docker-compose.yml down -v"
echo ""
