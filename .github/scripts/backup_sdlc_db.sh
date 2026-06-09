#!/usr/bin/env bash
# SDLC PostgreSQL 백업 스크립트
# 용도: 로컬 개발 환경에서 sdlc 스키마 백업 (Phase B 조건 4번)
#
# 사용법:
#   bash .github/scripts/backup_sdlc_db.sh
#
# 환경변수 (선택):
#   SDLC_DB_HOST     기본값: localhost
#   SDLC_DB_PORT     기본값: 5432
#   SDLC_DB_NAME     기본값: platforma_sdlc
#   SDLC_DB_USER     기본값: platforma
#   PGPASSWORD       미설정 시 platforma_dev_password 사용
#
# 출력: AI/backups/YYYY-MM-DD_sdlc.sql.gz
# 보관: 최근 7일치만 유지 (이전 파일 자동 삭제)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
BACKUP_DIR="${REPO_ROOT}/AI/backups"
DATE_TAG=$(date +%Y-%m-%d)
BACKUP_FILE="${BACKUP_DIR}/${DATE_TAG}_sdlc.sql.gz"

DB_HOST="${SDLC_DB_HOST:-localhost}"
DB_PORT="${SDLC_DB_PORT:-5432}"
DB_NAME="${SDLC_DB_NAME:-platforma_sdlc}"
DB_USER="${SDLC_DB_USER:-platforma}"
export PGPASSWORD="${PGPASSWORD:-platforma_dev_password}"

mkdir -p "${BACKUP_DIR}"

echo "[backup] ${DB_NAME} 백업 시작 → ${BACKUP_FILE}"

if ! command -v pg_dump &>/dev/null; then
    echo "[backup] WARN: pg_dump 미설치 - 백업 건너뜀" >&2
    exit 0
fi

pg_dump \
    -h "${DB_HOST}" \
    -p "${DB_PORT}" \
    -U "${DB_USER}" \
    -n sdlc \
    --no-owner \
    --no-acl \
    "${DB_NAME}" | gzip > "${BACKUP_FILE}"

SIZE=$(du -h "${BACKUP_FILE}" | cut -f1)
echo "[backup] 완료: ${BACKUP_FILE} (${SIZE})"

# 7일 이상 된 백업 삭제
find "${BACKUP_DIR}" -name "*_sdlc.sql.gz" -mtime +7 -delete
REMAINING=$(ls "${BACKUP_DIR}"/*_sdlc.sql.gz 2>/dev/null | wc -l)
echo "[backup] 보관 중: ${REMAINING}개"
