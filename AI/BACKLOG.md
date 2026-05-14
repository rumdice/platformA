# BACKLOG — 전체 작업 목록

> 우선순위는 사용자가 결정합니다.
> 태스크를 시작하면 SPRINT.md로 이동하십시오.

---

## 긴급 (보안/안정성)

### ~~#BACK-001: 설정값 환경변수 이전~~ ✅ 완료 (스프린트 #5, 2026-04-27)
JWT 시크릿·DB 비밀번호를 환경변수 fallback 패턴으로 이전. ADR-004 참조.

---

## 높음 (기능 완성)

### ~~#BACK-002: Matching API Dockerfile 추가~~ ✅ 완료 (스프린트 #19, 2026-05-13)
HTTPS 전용 포트 7002 Dockerfile 신규 생성. docker-compose.full.yml에도 포함.

### #BACK-003: 전체 스택 docker-compose.yml 작성
**배경**: Redis는 docker-compose 있지만 API 서비스들은 없음
**작업**:
- `docker-compose.yml` (루트) 작성: Auth, Ticketing, Matching, Utils, Game Server + Redis + MySQL
- 환경변수 주입, 헬스체크 depends_on 설정
- 로컬 개발 원클릭 실행 가능하도록
**영향 범위**: 신규 파일 추가
**예상 크기**: 중 (4시간)

### #BACK-004: 유닛 테스트 프로젝트 추가
**배경**: 현재 테스트 프로젝트 전무 (TESTING_STRATEGY.md 참조)
**작업**:
- `PlatformA.Library.Tests` 프로젝트 생성
- `TokenManager`, `RedisRateLimiterService`, `SnowflakeGenerator`, `Base62Converter` 유닛 테스트
- 패킷 직렬화/역직렬화 왕복 테스트
**영향 범위**: 신규 프로젝트 추가
**예상 크기**: 중 (8시간)

---

## 보통 (기능 개선)

### #BACK-005: Rating 기반 매칭 구현
**배경**: 현재 FIFO 방식만 지원. DummyClient 시나리오 6 미구현 상태
**작업**:
- `player_stats.rating` 컬럼 추가 (Migration 필요)
- 매칭 엔진 rating 범위 기반 페어링 로직
- DummyClient 시나리오 6 완성
**영향 범위**: Matching API, MySqlDB.Lib, DummyClient
**예상 크기**: 대 (16시간)

### #BACK-006: Utils API CORS 보안 강화
**배경**: 현재 `AllowAnyOrigin()` 설정 (보안 취약)
**작업**:
- `appsettings.json`에 허용 오리진 목록 설정
- 환경별(개발/스테이징/프로덕션) CORS 정책 분리
**영향 범위**: Utils API Program.cs
**예상 크기**: 소 (1시간)

### #BACK-007: 매칭 1:3 / 1:N 모드 지원
**배경**: 현재 1:1 (2인) 매칭만 지원
**작업**:
- 매칭 모드 enum 추가 (TwoPlayer, FourPlayer 등)
- 매칭 엔진 N인 그룹핑 로직
- API_CONTRACTS.md 업데이트
**영향 범위**: Matching API, Game Server (룸 생성 로직)
**예상 크기**: 중 (8시간)

---

## 낮음 (품질 개선)

### #BACK-008: 로그 구조화 개선
**배경**: log4net 사용 중이나 구조화 로그(JSON) 미적용
**작업**:
- `log4net.config` JSON 레이아웃 적용
- ELK Stack 또는 CloudWatch 연동 준비
**영향 범위**: 모든 API 프로젝트
**예상 크기**: 소 (2시간)

### ~~#BACK-009: Snowflake WorkerId 환경변수 주입~~ ✅ 완료 (스프린트 #5, 2026-04-27)
SNOWFLAKE_WORKER_ID / SNOWFLAKE_DATACENTER_ID 환경변수로 주입 가능하도록 변경.

### #BACK-010: 매치 기록 저장 구현
**배경**: `match_records` 테이블 존재하나 실제 기록 저장 로직 미구현
**작업**:
- 매칭 성공 시 `match_records` INSERT
- 게임 종료 시 `status`, `winner_id`, `ended_at` UPDATE
**영향 범위**: Matching API, Game Server
**예상 크기**: 중 (6시간)

---

## 완료된 항목

- **#BACK-001** 설정값 환경변수 이전 (스프린트 #5, 2026-04-27)
- **#BACK-009** Snowflake WorkerId 환경변수 주입 (스프린트 #5, 2026-04-27)
