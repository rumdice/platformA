@echo off
echo [1/3] 기존 Redis 컨테이너 정리 중...
docker-compose down

echo [2/3] Redis 노드 6개 실행 중 (3 Master + 3 Replica)...
docker-compose up -d

echo 컨테이너가 안정화될 때까지 5초간 대기합니다...
timeout /t 5 /nobreak > nul

echo [3/3] Redis 클러스터 구성 시작...
:: 각 노드의 내부 IP를 동적으로 가져와서 클러스터를 생성합니다.
for /f "tokens=*" %%i in ('docker inspect -f "{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}" redis-1') do set IP1=%%i
for /f "tokens=*" %%i in ('docker inspect -f "{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}" redis-2') do set IP2=%%i
for /f "tokens=*" %%i in ('docker inspect -f "{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}" redis-3') do set IP3=%%i
for /f "tokens=*" %%i in ('docker inspect -f "{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}" redis-4') do set IP4=%%i
for /f "tokens=*" %%i in ('docker inspect -f "{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}" redis-5') do set IP5=%%i
for /f "tokens=*" %%i in ('docker inspect -f "{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}" redis-6') do set IP6=%%i

docker exec -it redis-1 redis-cli --cluster create %IP1%:6379 %IP2%:6379 %IP3%:6379 %IP4%:6379 %IP5%:6379 %IP6%:6379 --cluster-replicas 1 --cluster-yes

echo.
echo ==========================================
echo Redis 클러스터 구성이 완료되었습니다!
echo 아래 명령어로 상태를 확인하세요:
echo docker exec -it redis-1 redis-cli cluster nodes
echo ==========================================
pause