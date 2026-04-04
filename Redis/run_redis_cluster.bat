@echo off
setlocal
chcp 65001 >nul

echo ==========================================
echo  Redis Cluster 자동 구성 스크립트
echo  3 Master + 3 Slave (6371~6376)
echo ==========================================
echo.

echo [1/7] 기존 컨테이너 제거...
docker compose down -v --remove-orphans
if %ERRORLEVEL% NEQ 0 (
    echo [오류] docker compose down 실패
    pause
    exit /b 1
)

echo [2/7] Redis 컨테이너 실행...
docker compose up -d
if %ERRORLEVEL% NEQ 0 (
    echo [오류] docker compose up 실패
    pause
    exit /b 1
)

echo [대기] 컨테이너 안정화 (10초)...
timeout /t 10 > nul

REM === 컨테이너 정상 기동 확인 ===
echo [검증] 컨테이너 상태 확인...
for %%i in (1 2 3 4 5 6) do (
    docker exec redis-%%i redis-cli -p 637%%i ping > nul 2>&1
    if !ERRORLEVEL! NEQ 0 (
        echo [오류] redis-%%i 가 응답하지 않습니다. 5초 추가 대기...
        timeout /t 5 > nul
        docker exec redis-%%i redis-cli -p 637%%i ping > nul 2>&1
        if !ERRORLEVEL! NEQ 0 (
            echo [오류] redis-%%i 기동 실패. 스크립트를 종료합니다.
            pause
            exit /b 1
        )
    )
)
echo [검증] 모든 노드 PONG 응답 확인 완료

echo.
echo [3/7] 클러스터 노드 연결 (MEET)...

REM docker 내부 네트워크에서는 컨테이너 이름으로 통신하되,
REM cluster-announce-ip=127.0.0.1 이므로 클라이언트(C#)는 localhost로 접속
docker exec redis-1 redis-cli -p 6371 cluster meet redis-2 6372
docker exec redis-1 redis-cli -p 6371 cluster meet redis-3 6373
docker exec redis-1 redis-cli -p 6371 cluster meet redis-4 6374
docker exec redis-1 redis-cli -p 6371 cluster meet redis-5 6375
docker exec redis-1 redis-cli -p 6371 cluster meet redis-6 6376

echo [대기] gossip 프로토콜 안정화 (8초)...
timeout /t 8 > nul

REM === 6개 노드 모두 인식되었는지 확인 ===
echo [검증] 클러스터 노드 수 확인...
FOR /F %%A IN ('docker exec redis-1 redis-cli -p 6371 cluster info ^| findstr cluster_known_nodes') DO SET KNOWN=%%A
echo %KNOWN%

echo.
echo [4/7] 슬롯 할당 (Master 3개, 배치 처리)...

REM addslots를 컨테이너 내부 sh -c 로 실행하여 명령줄 길이 제한 회피
REM redis-1: 0~5460
docker exec redis-1 sh -c "for i in $(seq 0 5460); do redis-cli -p 6371 cluster addslots \$i > /dev/null 2>&1; done"
echo   redis-1 (6371): 슬롯 0~5460 할당 완료

REM redis-2: 5461~10922
docker exec redis-2 sh -c "for i in $(seq 5461 10922); do redis-cli -p 6372 cluster addslots \$i > /dev/null 2>&1; done"
echo   redis-2 (6372): 슬롯 5461~10922 할당 완료

REM redis-3: 10923~16383
docker exec redis-3 sh -c "for i in $(seq 10923 16383); do redis-cli -p 6373 cluster addslots \$i > /dev/null 2>&1; done"
echo   redis-3 (6373): 슬롯 10923~16383 할당 완료

echo [대기] 슬롯 전파 안정화 (5초)...
timeout /t 5 > nul

echo.
echo [5/7] Master Node ID 조회...

FOR /F "tokens=1" %%A IN ('docker exec redis-1 redis-cli -p 6371 cluster myid') DO SET M1=%%A
FOR /F "tokens=1" %%A IN ('docker exec redis-2 redis-cli -p 6372 cluster myid') DO SET M2=%%A
FOR /F "tokens=1" %%A IN ('docker exec redis-3 redis-cli -p 6373 cluster myid') DO SET M3=%%A

echo   Master1 (redis-1): %M1%
echo   Master2 (redis-2): %M2%
echo   Master3 (redis-3): %M3%

if "%M1%"=="" (
    echo [오류] Master1 ID를 가져올 수 없습니다.
    pause
    exit /b 1
)
if "%M2%"=="" (
    echo [오류] Master2 ID를 가져올 수 없습니다.
    pause
    exit /b 1
)
if "%M3%"=="" (
    echo [오류] Master3 ID를 가져올 수 없습니다.
    pause
    exit /b 1
)

echo.
echo [6/7] Slave 연결 (Replica 구성)...

docker exec redis-4 redis-cli -p 6374 cluster replicate %M1%
echo   redis-4 (6374) → redis-1 (6371) slave 연결
docker exec redis-5 redis-cli -p 6375 cluster replicate %M2%
echo   redis-5 (6375) → redis-2 (6372) slave 연결
docker exec redis-6 redis-cli -p 6376 cluster replicate %M3%
echo   redis-6 (6376) → redis-3 (6373) slave 연결

echo [대기] 최종 안정화 (5초)...
timeout /t 5 > nul

echo.
echo [7/7] 클러스터 상태 확인...
echo.
echo --- cluster info ---
docker exec redis-1 redis-cli -p 6371 cluster info
echo.
echo --- cluster nodes ---
docker exec redis-1 redis-cli -p 6371 cluster nodes

REM === 최종 상태 검증 ===
echo.
FOR /F "tokens=2 delims=:" %%A IN ('docker exec redis-1 redis-cli -p 6371 cluster info ^| findstr cluster_state') DO SET STATE=%%A
REM 캐리지 리턴 제거
SET STATE=%STATE: =%

echo ==========================================
if "%STATE%"=="ok" (
    echo [성공] Redis Cluster 구성 완료!
) else (
    echo [경고] cluster_state=%STATE%
    echo        슬롯 할당이 완료되지 않았을 수 있습니다.
    echo        잠시 후 다시 확인해 주세요.
)
echo.
echo  구성: 3 Master + 3 Slave
echo  Master: 127.0.0.1:6371, 6372, 6373
echo  Slave:  127.0.0.1:6374, 6375, 6376
echo.
echo  C# 연결 문자열:
echo  127.0.0.1:6371,127.0.0.1:6372,127.0.0.1:6373,127.0.0.1:6374,127.0.0.1:6375,127.0.0.1:6376
echo ==========================================

pause
