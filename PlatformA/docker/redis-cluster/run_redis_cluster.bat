@echo off
setlocal
chcp 65001 >nul

echo [1/3] 기존 Redis 컨테이너 및 볼륨 제거 중...
:: -v 옵션을 주어야 이전 클러스터 정보(nodes.conf)가 확실히 지워집니다.
docker-compose down -v

echo [2/3] Redis 노드 6개 실행 중...
docker-compose up -d

echo [대기] 컨테이너 클러스터 체결 안정화 (10초)...
timeout /t 10 > nul

echo [3/3] Cluster 구성을 확인한다.
docker exec -it redis-master-1 redis-cli -p 6371 cluster nodes

pause