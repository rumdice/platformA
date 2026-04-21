# TESTING_STRATEGY — 테스트 전략

---

## 현재 상태

**유닛 테스트 프로젝트 없음** (기술 부채 — BACKLOG #BACK-004)

현재 테스트 방법:
- **기능 테스트**: `PlatformA.Game.DummyClient` 시나리오 실행
- **수동 검증**: Swagger UI, curl, Postman

---

## DummyClient 시나리오 목록

```bash
cd /home/user/platformA/PlatformA/PlatformA.Game.DummyClient
dotnet run
```

| 번호 | 시나리오 | 검증 항목 |
|------|---------|---------|
| 1 | Game Server TCP 연결 테스트 | TCP 소켓 연결, 패킷 송수신 |
| 2 | Utils API 프론트 테스트 | IP 조회, URL 단축 |
| 3 | 단일 유저 로그인 → 매칭 | Auth → Ticketing → Matching → Game 전체 흐름 |
| 4 | 1,000명 로그인 + 대기열 처리량 | 대기열 성능, Rate Limit 동작 |
| 5 | 1,000명 + 매칭 시스템 부하 | 매칭 엔진 성능 (WIP) |
| 6 | Rating 기반 매칭 테스트 | Rating 매칭 (WIP) |
| 7 | 단일 유저 통합 로그인/재로그인 | Token Refresh, 재접속 흐름 |
| 8 | 중복 로그인 방지 테스트 | 분산 락, 중복 접속 차단 |

---

## 기능별 검증 방법

### Auth API 검증
```bash
# 로그인 (신규 유저 자동 등록)
curl -X POST https://localhost:7088/api/Auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"pass1234"}' \
  -k

# Token Refresh
curl -X POST https://localhost:7088/api/Auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"<refresh_token>"}' \
  -k

# Rate Limit 검증 (11번째 요청 → 429)
for i in {1..11}; do
  curl -s -o /dev/null -w "%{http_code}\n" \
    -X POST https://localhost:7088/api/Auth/login \
    -H "Content-Type: application/json" \
    -d '{"username":"testuser","password":"pass1234"}' -k
done
```

### Ticketing API 검증
```bash
TOKEN="<access_token>"

# 대기열 진입
curl -X POST https://localhost:7075/api/queue/enter \
  -H "Authorization: Bearer $TOKEN" -k

# 상태 폴링
curl -X GET https://localhost:7075/api/queue/status \
  -H "Authorization: Bearer $TOKEN" -k

# 대기열 이탈
curl -X POST https://localhost:7075/api/queue/leave \
  -H "Authorization: Bearer $TOKEN" -k
```

### 헬스체크 검증
```bash
# 모든 API liveness 확인
curl -f http://localhost:7088/healthz && echo "Auth: OK"
curl -f http://localhost:7075/healthz && echo "Ticketing: OK"
curl -f http://localhost:5189/healthz && echo "Matching: OK"

# readiness 확인 (Redis + DB 포함)
curl http://localhost:7088/readyz
curl http://localhost:7075/readyz
```

### Game Server 검증
DummyClient 시나리오 1 또는 3 실행:
```bash
dotnet run --project PlatformA.Game.DummyClient
# 메뉴에서 1 또는 3 선택
```

---

## 빌드 검증 (최소 기준)

모든 코드 변경 후 반드시:

```bash
cd /home/user/platformA/PlatformA
dotnet build PlatformA.sln
```

빌드 실패 = 커밋 금지.

---

## 향후 테스트 계획 (BACKLOG #BACK-004)

### 단계별 도입 계획

**1단계: 유닛 테스트 프로젝트 추가**
```
PlatformA.Library.Tests/
PlatformA.Auth.API.Tests/
```

**우선순위 테스트 대상:**
1. `TokenManager.GenerateJwtToken()` + `ValidateTokenAndGetUserId()`
2. `RedisRateLimiterService.IsAllowedAsync()` 슬라이딩 윈도우 로직
3. `SnowflakeGenerator.NextId()` 유니크 보장
4. `Base62Converter.Encode()` / `Decode()` 왕복
5. 패킷 직렬화/역직렬화 왕복 (Source Generator 검증)

**2단계: 통합 테스트**
- Auth 흐름 end-to-end (TestContainers로 MySQL + Redis)
- 대기열 진입/이탈/Active 전환 시나리오

**3단계: 부하 테스트**
- DummyClient 시나리오 4, 5 자동화
- k6 또는 NBomber 도입 검토

---

## 커버리지 기준 (미래)

| 영역 | 최소 목표 |
|------|---------|
| 비즈니스 로직 (Services) | 80% |
| 컨트롤러 | 60% |
| 패킷 직렬화 | 100% |
| Redis 키 로직 | 70% |
