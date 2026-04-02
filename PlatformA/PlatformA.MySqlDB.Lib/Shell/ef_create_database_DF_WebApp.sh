#!/bin/bash
set -e

cd "$(dirname "$0")/.."
echo "[WebApp DF] DB에서 엔티티 역방향 생성 (Database-First)..."
dotnet ef dbcontext scaffold \
  "Server=localhost;Port=3306;Database=db_WebApp;User=root;Password=pass1234" \
  Pomelo.EntityFrameworkCore.MySql \
  --output-dir DBWebApp \
  --force \
  --context DbWebAppContext \
  --use-database-names \
  --no-pluralize \
  --no-onconfiguring \
  --project . \
  --startup-project .

echo "[WebApp DF] 빌드 확인..."
dotnet build
