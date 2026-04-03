.bat 파일 실행


클러스터 모드 확인
docker exec -it redis-1 redis-cli cluster nodes


클러스터 모드가 아니면 연결 시키기
docker exec -it redis-1 redis-cli --cluster create redis-1:6379 redis-2:6379 redis-3:6379 redis-4:6379 redis-5:6379 redis-6:6379 --cluster-replicas 1 --cluster-yes


재확인
docker exec -it redis-1 redis-cli cluster nodes
