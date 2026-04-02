#!/bin/bash
# ============================================================
# Code-First : 엔티티 코드 기준으로 DB 스키마를 생성합니다.
#
# 사용 흐름:
#   1) DBWebApp/Entities/*.cs 파일을 수정
#   2) 이 스크립트 실행 → Migrations/WebApp 폴더에 마이그레이션 생성
#   3) dotnet ef database update 로 실제 DB에 반영
#
# 대상 테이블: players, player_stats, match_records
# ============================================================
set -e

cd "$(dirname "$0")/.."

echo "[WebApp CF] 기존 마이그레이션 폴더 삭제..."
rm -rf Migrations/WebApp

echo "[WebApp CF] 기존 DB 삭제 (주의: 데이터 전체 소멸)..."
dotnet ef database drop --force --context DbWebAppContext --project . --startup-project .

echo "[WebApp CF] 마이그레이션 생성..."
dotnet ef migrations add InitialCreate \
  --context DbWebAppContext \
  --output-dir Migrations/WebApp \
  --project . --startup-project .

echo "[WebApp CF] DB 업데이트 (스키마 적용)..."
dotnet ef database update --context DbWebAppContext --project . --startup-project .

echo "[WebApp CF] 빌드 확인..."
dotnet build

echo "[WebApp CF] 완료."
