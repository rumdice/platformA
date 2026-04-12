@echo off
for /L %%p in (6371, 1, 6373) do (
    docker exec -it redis-master-1 redis-cli -p %%p FLUSHALL
)
pause