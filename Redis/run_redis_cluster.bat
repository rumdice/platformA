@echo off
setlocal
chcp 65001 >nul

echo [1/3] 기존 Redis 컨테이너 및 볼륨 제거 중...
:: -v 옵션을 주어야 이전 클러스터 정보(nodes.conf)가 확실히 지워집니다.
docker-compose down -v

echo [2/3] Redis 노드 6개 실행 중...
docker-compose up -d

echo 컨테이너가 안정화될 때까지 10초간 대기합니다...
timeout /t 10 /nobreak > nul

echo [3/3] Redis 클러스터 구성 시작 (Hostname 방식)...
:: host.docker.internal을 사용하여 클러스터를 묶습니다.
docker exec -it redis-1 redis-cli --cluster create ^
  host.docker.internal:6371 ^
  host.docker.internal:6372 ^
  host.docker.internal:6373 ^
  host.docker.internal:6374 ^
  host.docker.internal:6375 ^
  host.docker.internal:6376 ^
  --cluster-replicas 1 --cluster-yes

echo.
echo ==========================================
echo Redis 클러스터 구성이 완료되었습니다!
echo 상태 확인: docker exec -it redis-1 redis-cli cluster nodes
echo ==========================================
docker exec -it redis-1 redis-cli cluster nodes


echo.
echo ==========================================
echo 로컬 환경에서 redis 연결시 hosts 파일 관리자 권한으로 수정 필요.
echo 127.0.0.1 host.docker.internal 추가 및 기존 docker desktop 옵션 주석처리
echo ==========================================
pause