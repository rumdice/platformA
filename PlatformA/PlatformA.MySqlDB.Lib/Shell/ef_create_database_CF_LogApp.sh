#!/bin/bash
# ============================================================
# Code-First : 엔티티 코드 기준으로 DB 스키마를 생성합니다.
#
# 사용 흐름:
#   1) DBLogApp/Entities/*.cs 파일을 수정
#   2) 이 스크립트 실행 → Migrations/LogApp 폴더에 마이그레이션 생성
#   3) dotnet ef database update 로 실제 DB에 반영
#
# 대상 테이블: access_logs
# ============================================================
set -e

cd "$(dirname "$0")/.."

echo "[LogApp CF] 기존 마이그레이션 폴더 삭제..."
rm -rf Migrations/LogApp

echo "[LogApp CF] 기존 DB 삭제 (주의: 데이터 전체 소멸)..."
dotnet ef database drop --force --context DbLogAppContext --project . --startup-project .

echo "[LogApp CF] 마이그레이션 생성..."
dotnet ef migrations add InitialCreate \
  --context DbLogAppContext \
  --output-dir Migrations/LogApp \
  --project . --startup-project .

echo "[LogApp CF] DB 업데이트 (스키마 적용)..."
dotnet ef database update --context DbLogAppContext --project . --startup-project .

echo "[LogApp CF] 빌드 확인..."
dotnet build

echo "[LogApp CF] 완료."
