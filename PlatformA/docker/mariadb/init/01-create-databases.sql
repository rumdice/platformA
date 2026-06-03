-- PlatformA 필수 데이터베이스 자동 생성
-- MariaDB 컨테이너 최초 기동 시 /docker-entrypoint-initdb.d/ 에서 실행됨.
-- mariadb-data 볼륨에 데이터가 이미 존재하면 이 스크립트는 실행되지 않으므로
-- 기존 데이터에 영향을 주지 않는다.

CREATE DATABASE IF NOT EXISTS `db_WebApp`
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_uca1400_ai_ci;

CREATE DATABASE IF NOT EXISTS `db_LogApp`
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_uca1400_ai_ci;
