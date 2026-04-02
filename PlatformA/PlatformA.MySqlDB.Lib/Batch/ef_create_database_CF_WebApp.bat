@echo off
cd /d "%~dp0.."

echo [WebApp CF] 기존 마이그레이션 폴더 삭제...
if exist "Migrations\WebApp" rmdir /s /q "Migrations\WebApp"

echo [WebApp CF] 기존 DB 삭제...
dotnet ef database drop --force --context DbWebAppContext --project . --startup-project .

echo [WebApp CF] 마이그레이션 생성...
dotnet ef migrations add InitialCreate --context DbWebAppContext --output-dir Migrations\WebApp --project . --startup-project .

echo [WebApp CF] DB 업데이트...
dotnet ef database update --context DbWebAppContext --project . --startup-project .

echo [WebApp CF] 빌드 확인...
dotnet build
