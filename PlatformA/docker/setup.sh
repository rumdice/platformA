#!/usr/bin/env bash
# PlatformA Docker 환경 최초 설정 스크립트 (Linux / macOS)
# 실행: chmod +x docker/setup.sh && ./docker/setup.sh
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CERTS_DIR="$SCRIPT_DIR/certs"
CERT_FILE="$CERTS_DIR/devcert.pfx"
CERT_PASSWORD="${CERT_PASSWORD:-localdev}"

echo "[Setup] PlatformA Docker 환경을 초기화합니다."

# 1. certs 디렉터리 생성
mkdir -p "$CERTS_DIR"

# 2. 개발 인증서 생성 (이미 있으면 건너뜀)
if [ -f "$CERT_FILE" ]; then
  echo "[Setup] 개발 인증서가 이미 존재합니다: $CERT_FILE"
  echo "[Setup] 재생성하려면 파일을 삭제 후 다시 실행하세요."
else
  if ! command -v dotnet &>/dev/null; then
    echo "[Error] dotnet SDK가 설치되지 않았습니다."
    echo "        https://dotnet.microsoft.com/download 에서 .NET 10 SDK를 설치하세요."
    exit 1
  fi
  echo "[Setup] 개발 인증서를 생성합니다..."
  dotnet dev-certs https --trust 2>/dev/null || true
  dotnet dev-certs https --export-path "$CERT_FILE" --password "$CERT_PASSWORD"
  echo "[Setup] 인증서 생성 완료: $CERT_FILE"
fi

# 3. .env 파일 생성 (없는 경우에만)
ENV_FILE="$SCRIPT_DIR/.env"
ENV_EXAMPLE="$SCRIPT_DIR/.env.example"
if [ ! -f "$ENV_FILE" ] && [ -f "$ENV_EXAMPLE" ]; then
  cp "$ENV_EXAMPLE" "$ENV_FILE"
  echo "[Setup] .env 파일 생성 완료. 필요 시 수정하세요: $ENV_FILE"
fi

PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
echo ""
echo "[Setup] 완료! 이제 다음 명령어로 전체 스택을 시작하세요:"
echo ""
echo "  cd $PROJECT_DIR"
echo "  docker compose -f docker/docker-compose.full.yml up -d --build"
echo ""
