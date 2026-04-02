@echo off
cd /d "%~dp0.."

echo [LogApp DF] DB에서 엔티티 역방향 생성 (Database-First)...
dotnet ef dbcontext scaffold ^
  "Server=localhost;Port=3306;Database=db_LogApp;User=root;Password=pass1234" ^
  Pomelo.EntityFrameworkCore.MySql ^
  --output-dir DBLogApp ^
  --force ^
  --context DbLogAppContext ^
  --use-database-names ^
  --no-pluralize ^
  --no-onconfiguring ^
  --project . ^
  --startup-project .

echo [LogApp DF] 빌드 확인...
dotnet build
