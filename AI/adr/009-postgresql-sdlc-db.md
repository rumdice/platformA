# ADR-009: PostgreSQL SDLC 전용 DB 채택

## 상태: 확정

## 날짜: 2026-06-03

---

## 맥락

AI_SDLC Phase 3 인프라(ADR-008 n8n)를 위한 OLTP 데이터베이스가 필요해졌다.
n8n은 내부 메타데이터(워크플로, 실행 이력, 인증 정보 등)를 영속 저장소에 보관하며,
PostgreSQL을 공식 권장 운영 DB로 지정하고 있다.

게임 서비스는 MariaDB를 사용 중이나, SDLC 데이터를 게임 DB 인스턴스에 혼재시키면:
- 게임 서비스 장애가 SDLC 파이프라인에 전파될 수 있음
- 스키마·마이그레이션 관리 책임이 혼재되어 운영 복잡도 증가
- n8n이 MariaDB를 공식 지원하지 않아 데이터 타입 호환성 문제 발생 가능

---

## 결정

**PostgreSQL 16 채택 — SDLC 전용 독립 인스턴스**

- 이미지: `postgres:16`
- 컨테이너명: `platforma-sdlc-postgres`
- 포트: `5432`
- 데이터베이스: `platforma_sdlc` (환경변수 `POSTGRES_DB`)
- 스키마 분리: n8n 메타데이터는 `n8n` 스키마에 저장 (`DB_POSTGRESDB_SCHEMA: n8n`)
- 배포: `docker/postgresql/docker-compose.yml` (독립), `docker/docker-compose.full.yml` (통합)
- healthcheck: `pg_isready` 기반, n8n 서비스는 healthy 상태 확인 후 기동

---

## 대안과 기각 이유

| 대안 | 기각 이유 |
|------|---------|
| MariaDB 동일 인스턴스 재사용 | 게임 서비스 DB와 SDLC 혼재로 장애 격리 불가. n8n이 MariaDB를 공식 지원하지 않아 데이터 타입·쿼리 호환성 문제 발생 가능 |
| SQLite (n8n 기본) | 멀티 컨테이너 환경에서 파일 공유 불가. n8n 공식 문서가 운영 환경에서 SQLite 미권장. 동시성 제한 |
| MySQL | MariaDB와 유사한 특성으로 동일한 격리 문제 존재. n8n 권장 DB는 PostgreSQL |
| Redis만 사용 | 영속성 보장이 어렵고 복잡한 쿼리(JOIN, JSONB 검색 등) 불가. n8n 메타데이터 저장 용도 부적합 |

---

## 결과 및 트레이드오프

**이득:**
- n8n 공식 권장 DB로 데이터 타입·쿼리 완전 호환 보장
- 게임 서비스 MariaDB와 완전 격리 — 상호 장애 전파 없음
- JSONB, 배열 등 고급 타입 지원으로 향후 SDLC 데이터 모델 확장 용이
- 스키마 기반 논리적 분리 (`n8n` 스키마 등) 가능

**비용 / 제약:**
- 추가 컨테이너 운영 부담 (MariaDB와 별도 모니터링·백업 정책 수립 필요)
- 포트 5432 호스트 노출 관리 필요
- 게임 서비스 개발자가 익숙하지 않을 경우 학습 비용 발생 (MariaDB ↔ PostgreSQL SQL 방언 차이)

---

## 변경 방법

이 결정을 변경하려면:
1. 새 ADR 작성 (`AI/adr/NNN-제목.md`)
2. 사용자 승인 후 진행
3. 이 ADR 상태를 `대체됨 (→ ADR-NNN)`으로 업데이트
