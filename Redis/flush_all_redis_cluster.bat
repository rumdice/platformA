foreach ($port in 6371..6373) { docker exec -it redis-master-1 redis-cli -p $port FLUSHALL }

pause