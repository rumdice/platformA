# ADR-008: n8n 이벤트 오케스트레이터 채택

## 상태: 확정

## 날짜: 2026-06-03

---

## 맥락

AI_SDLC Phase 3에서 Git/CI/AI/알림 이벤트를 자동화할 워크플로 엔진이 필요해졌다.
기존에는 이벤트별로 수동 처리하거나 C# BackgroundService로 일회성 스크립트를 작성했으나,
자동화할 이벤트 종류가 증가(PR 머지 감지, 빌드 실패 알림, AI 리포트 스케줄링 등)함에 따라
재사용 가능한 전용 오케스트레이터가 요구됐다.

요구 조건:
- self-hosted: 소스 코드·토큰이 외부로 유출되지 않아야 함
- 로컬 개발 환경에서 Docker Compose로 즉시 실행 가능
- GitHub, Slack, Webhook 등 외부 서비스와 연동 가능
- 비개발자도 워크플로를 시각적으로 확인·수정 가능

---

## 결정

**n8n 채택 — self-hosted low-code 워크플로 엔진**

- 배포: Docker Compose (`docker/n8n/docker-compose.yml` 독립, `docker/docker-compose.full.yml` 통합)
- 백엔드: PostgreSQL (ADR-009) — `n8n` 스키마에 메타데이터 저장
- 접근 URL: `http://localhost:5678`
- 인증: Basic Auth (`N8N_BASIC_AUTH_USER` / `N8N_BASIC_AUTH_PASSWORD` 환경변수)
- 타임존: `Asia/Seoul`

---

## 대안과 기각 이유

| 대안 | 기각 이유 |
|------|---------|
| Temporal | Go/Java SDK 기반, .NET SDK 미성숙. 워커 프로세스 별도 운영 필요. 로컬 개발 환경 구성 복잡 |
| Apache Airflow | Python 전용 DAG 작성 필요. .NET 생태계와 이질적. 데이터 파이프라인 특화로 이벤트 오케스트레이션 용도와 맞지 않음 |
| AWS Step Functions | 벤더 락인. 로컬 개발 불가(에뮬레이터 제한적). 이벤트 수에 비례한 비용 발생 |
| C# BackgroundService 직접 구현 | 이벤트 종류 증가 시마다 코드 수정·배포 필요. GUI 없어 시각적 관리 불가. 외부 서비스 연동 코드 직접 작성 부담 |

---

## 결과 및 트레이드오프

**이득:**
- 로우코드 GUI로 워크플로 시각화·관리 — 비개발자도 편집 가능
- GitHub, Slack, Webhook, HTTP 등 200+ 내장 노드로 연동 코드 불필요
- self-hosted로 소스 코드·시크릿이 외부 서비스에 노출되지 않음
- Docker Compose 한 줄로 로컬 실행 가능

**비용 / 제약:**
- n8n 버전 업그레이드 시 워크플로 하위 호환성 수동 확인 필요
- 복잡한 비즈니스 로직은 코드 노드(JavaScript)로 작성해야 하며 버전 관리가 어려움
- SSO, 감사 로그 등 Enterprise 기능은 유료 라이선스 필요
- 독립 실행(standalone) 시 SQLite 백엔드 사용 — 프로덕션 전환 시 PostgreSQL 마이그레이션 필요

---

## 변경 방법

이 결정을 변경하려면:
1. 새 ADR 작성 (`AI/adr/NNN-제목.md`)
2. 사용자 승인 후 진행
3. 이 ADR 상태를 `대체됨 (→ ADR-NNN)`으로 업데이트
