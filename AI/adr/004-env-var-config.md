# ADR-004: 민감 설정값 환경변수 이전 (ADR-003 2단계 적용)

## 상태: 확정

## 날짜: 2026-04-27

---

## 맥락

ADR-003에서 개발 편의를 위해 `Consts.cs`에 모든 설정값을 하드코딩하기로 결정했으나,
JWT 시크릿·DB 비밀번호가 소스코드에 평문 노출되는 보안 위험이 존재함.
ADR-003이 명시한 개선 2단계(환경변수 이전)를 적용한다.

---

## 결정

`Consts.cs` 내 민감 설정 3개와 Snowflake WorkerId를 환경변수로 이전.
`const string` → `static readonly string` + `Environment.GetEnvironmentVariable()` fallback 패턴 적용.

**변경 원칙:**
- 로컬 개발 시 환경변수 미설정이면 기존 개발용 값으로 fallback → 로컬 개발 영향 없음
- 호출부(`Program.cs` 등) 변경 없음 — `Consts.XXX` 참조 방식 유지
- 프로덕션 배포 시 실제 시크릿을 환경변수로 주입

---

## 적용된 환경변수

| 환경변수 | 대상 | fallback (로컬 개발용) |
|---------|------|-----------------------|
| `JWT_SECRET` | JWT 서명 키 | 기존 개발용 키 |
| `MYSQL_WEBAPP_CONNECTION_STRING` | WebApp DB 연결 문자열 | localhost:3306 개발 DB |
| `MYSQL_LOGAPP_CONNECTION_STRING` | LogApp DB 연결 문자열 | localhost:3306 개발 DB |
| `SNOWFLAKE_WORKER_ID` | Snowflake 워커 ID | `1` |
| `SNOWFLAKE_DATACENTER_ID` | Snowflake 데이터센터 ID | `1` |

---

## 변경하지 않은 항목

- `REDIS_CONNECTION_STRING` — localhost 주소로 보안 위험 낮음, 3단계(AWS) 적용 시 처리
- `GAME_SERVER_IP` / `GAME_SERVER_PORT` — DummyClient 전용, 프로덕션 경로 아님
- URL 상수들 (`AUTH_API_URL` 등) — localhost 개발용, 민감 정보 아님

---

## 프로덕션 배포 시 설정 방법

**Docker:**
```bash
docker run -e JWT_SECRET="..." \
           -e MYSQL_WEBAPP_CONNECTION_STRING="..." \
           -e MYSQL_LOGAPP_CONNECTION_STRING="..." \
           platformA-auth:latest
```

**docker-compose.yml:**
```yaml
environment:
  - JWT_SECRET=${JWT_SECRET}
  - MYSQL_WEBAPP_CONNECTION_STRING=${MYSQL_WEBAPP_CONNECTION_STRING}
```

---

## 다음 단계 (ADR-003 3단계, 선택)

프로덕션 환경에서 AWS Secrets Manager / Parameter Store 연동.
현재는 미적용. 필요 시 새 ADR 작성 후 적용.
