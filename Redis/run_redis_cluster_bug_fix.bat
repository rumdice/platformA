@echo off
setlocal
chcp 65001 >nul

echo [1/4] 기존 찌꺼기 컨테이너 및 볼륨 완벽 제거 중...
docker-compose down -v --remove-orphans

echo [2/4] Redis 노드 6개 실행 중...
docker-compose up -d

echo 컨테이너가 안정화될 때까지 15초간 대기합니다...
timeout /t 15 /nobreak > nul

echo [3/4] Redis Master 노드 3개 구성 (무한대기 및 IP문제 우회)...
:: 복제본 없이 마스터 3개로만 먼저 클러스터를 묶습니다.
docker exec redis-1 redis-cli --cluster create host.docker.internal:6371 host.docker.internal:6372 host.docker.internal:6373 --cluster-yes

echo Master 노드 안정화를 위해 5초 대기...
timeout /t 5 /nobreak > nul

echo [4/4] Redis Slave(복제본) 노드 3개 추가...
:: 나머지 노드들을 클러스터에 슬레이브로 합류시킵니다.
docker exec redis-1 redis-cli --cluster add-node host.docker.internal:6374 host.docker.internal:6371 --cluster-slave
docker exec redis-1 redis-cli --cluster add-node host.docker.internal:6375 host.docker.internal:6371 --cluster-slave
docker exec redis-1 redis-cli --cluster add-node host.docker.internal:6376 host.docker.internal:6371 --cluster-slave

echo.
echo ==========================================
echo Redis 클러스터 구성이 완료되었습니다!
echo 상태 확인: 
echo ==========================================
docker exec redis-1 redis-cli cluster info
docker exec redis-1 redis-cli cluster nodes

echo.
echo ==========================================
echo 로컬 환경에서 redis 연결시 hosts 파일 관리자 권한으로 수정 필요.
echo 127.0.0.1 host.docker.internal 추가 및 기존 docker desktop 옵션 주석처리
echo ==========================================
pause