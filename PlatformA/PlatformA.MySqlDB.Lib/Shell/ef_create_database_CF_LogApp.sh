#!/bin/bash
set -e

cd "$(dirname "$0")/.."
echo "[LogApp CF] 기존 마이그레이션 폴더 삭제..."
rm -rf Migrations/LogApp

echo "[LogApp CF] 기존 DB 삭제..."
dotnet ef database drop --force --context DbLogAppContext --project . --startup-project .

echo "[LogApp CF] 마이그레이션 생성..."
dotnet ef migrations add InitialCreate \
  --context DbLogAppContext \
  --output-dir Migrations/LogApp \
  --project . \
  --startup-project .

echo "[LogApp CF] DB 업데이트..."
dotnet ef database update --context DbLogAppContext --project . --startup-project .

echo "[LogApp CF] 빌드 확인..."
dotnet build
