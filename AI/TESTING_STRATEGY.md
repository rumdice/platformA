# TESTING_STRATEGY — 테스트 전략

---

## 현재 상태

| 테스트 프로젝트 | 범위 |
|----------------|------|
| `PlatformA.Tests.Utils.API` | Utils API 컨트롤러 통합 + 유틸리티 유닛 (스프린트 #2) |
| `PlatformA.Tests.Auth.API` | Auth API 컨트롤러 통합 + DTO 유닛 (스프린트 #3) |
| `PlatformA.Tests.Game.Server` | Protobuf 패킷 round-trip (스프린트 #11) |

**미구현** (BACKLOG #BACK-004): Ticketing API, Matching API 테스트 프로젝트

추가 검증 방법:
- **기능 테스트**: `PlatformA.Game.DummyClient` 시나리오 실행 (`/run-scenarios`)
- **수동 검증**: Swagger UI, curl, Postman

---

## DummyClient 시나리오 목록

```bash
cd PlatformA/PlatformA.Game.DummyClient
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
curl -X POST https://localhost:7001/api/Auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"pass1234"}' \
  -k

# Token Refresh
curl -X POST https://localhost:7001/api/Auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"<refresh_token>"}' \
  -k

# Rate Limit 검증 (11번째 요청 → 429)
for i in {1..11}; do
  curl -s -o /dev/null -w "%{http_code}\n" \
    -X POST https://localhost:7001/api/Auth/login \
    -H "Content-Type: application/json" \
    -d '{"username":"testuser","password":"pass1234"}' -k
done
```

### Ticketing API 검증
```bash
TOKEN="<access_token>"

# 대기열 진입
curl -X POST https://localhost:7003/api/queue/enter \
  -H "Authorization: Bearer $TOKEN" -k

# 상태 폴링
curl -X GET https://localhost:7003/api/queue/status \
  -H "Authorization: Bearer $TOKEN" -k

# 대기열 이탈
curl -X POST https://localhost:7003/api/queue/leave \
  -H "Authorization: Bearer $TOKEN" -k
```

### 헬스체크 검증
```bash
# 모든 API liveness 확인
curl -f https://localhost:7001/healthz && echo "Auth: OK"
curl -f https://localhost:7003/healthz && echo "Ticketing: OK"
curl -f https://localhost:7002/healthz && echo "Matching: OK"

# readiness 확인 (Redis + DB 포함)
curl https://localhost:7001/readyz
curl https://localhost:7003/readyz
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
cd PlatformA
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
5. 패킷 직렬화/역직렬화 왕복 (Protobuf round-trip 검증)

**2단계: 통합 테스트**
- Auth 흐름 end-to-end (TestContainers로 MySQL + Redis)
- 대기열 진입/이탈/Active 전환 시나리오

**3단계: 부하 테스트**
- DummyClient 시나리오 4, 5 자동화
- k6 또는 NBomber 도입 검토

---

---

## Utils.API 유닛 테스트 명세 (스프린트 #2)

### 프로젝트: `PlatformA.Tests.Utils.API`

**테스트 프레임워크**: xUnit + Moq + Microsoft.AspNetCore.Mvc.Testing
**대상 프로젝트**: `PlatformA.Utils.API`, `PlatformA.Library`

---

### 발견된 버그 (테스트 작성 전 수정 필요)

| # | 위치 | 문제 | 수정 방법 |
|---|------|------|---------|
| B-1 | `FactAttribute.cs` | xUnit의 `[Fact]`를 로컬 빈 클래스로 섀도잉 → 테스트 미실행 | 파일 삭제 |
| B-2 | `Program.cs` | `IConnectionMultiplexer` DI 미등록 → 컨트롤러 DI 오류 | `RedisManager.Connection` 추가 등록 |

---

### 테스트 클래스 목록

#### 1. `Base62ConverterTests` — 순수 단위 테스트

| 테스트 메서드 | 검증 내용 | 입력 | 기대값 |
|-------------|---------|------|--------|
| `Encode_KnownValue_ReturnsCorrectString` | 알려진 값 인코딩 | `62L` | `"10"` |
| `Encode_One_ReturnsOne` | 1 인코딩 | `1L` | `"1"` |
| `Encode_LargeValue_ReturnsString` | 큰 값도 처리 | `9999999999L` | 비어있지 않음 |
| `Decode_EncodedValue_RoundTrip` | 왕복 일치 | 임의의 long | `Decode(Encode(n)) == n` |
| `Encode_Decode_MultipleValues_AreConsistent` | 다수 값 왕복 | 1,62,3844,100000 | 모두 일치 |

#### 2. `SnowflakeGeneratorTests` — 순수 단위 테스트

| 테스트 메서드 | 검증 내용 |
|-------------|---------|
| `Constructor_InvalidWorkerId_ThrowsException` | workerId > 31 → `ArgumentException` |
| `Constructor_InvalidDatacenterId_ThrowsException` | datacenterId > 31 → `ArgumentException` |
| `Constructor_NegativeWorkerId_ThrowsException` | workerId < 0 → `ArgumentException` |
| `NextId_ReturnsPositiveNumber` | 생성 ID > 0 |
| `NextId_MultipleCallsAreUnique` | 1000개 ID 모두 유니크 |
| `NextId_MultipleCallsAreMonotonicallyIncreasing` | 순차 ID 단조 증가 |
| `NextId_ConcurrentCalls_AreUnique` | 100 스레드 × 10회 = 1000개 유니크 |

#### 3. `UtilControllerTests` — 통합 테스트 (WebApplicationFactory)

**테스트 픽스처 설정:**
- SQLite 인메모리 DB (`Data Source=:memory:`)
- `IConnectionMultiplexer` Moq 목업
- `StatSyncsService` 비활성화 (Redis 실제 접속 차단)

| 테스트 메서드 | HTTP | 경로 | 기대 결과 |
|-------------|------|------|---------|
| `GetMyIp_Returns200_WithIpFields` | GET | `/util/myip` | 200, `ip` 필드 존재 |
| `ShortenUrl_ValidUrl_Returns200_WithShortUrl` | POST | `/util/shorten` | 200, `shortUrl` + `code` 반환 |
| `ShortenUrl_InvalidUrl_Returns400` | POST | `/util/shorten` | 400 |
| `ShortenUrl_EmptyUrl_Returns400` | POST | `/util/shorten` | 400 |
| `RedirectUrl_KnownCode_Returns302` | GET | `/go/{code}` | 302 리다이렉트 |
| `RedirectUrl_UnknownCode_Returns404` | GET | `/go/notexist` | 404 |
| `GetStats_KnownCode_Returns200_WithClickCount` | GET | `/util/stats/{code}` | 200, `clickCount` 필드 |
| `GetStats_UnknownCode_Returns404` | GET | `/util/stats/notexist` | 404 |

---

### 실행 명령

```bash
cd PlatformA
dotnet test PlatformA.Tests.Utils.API/PlatformA.Tests.Utils.API.csproj -v normal

# 커버리지 포함
dotnet test PlatformA.Tests.Utils.API/PlatformA.Tests.Utils.API.csproj \
  --collect:"XPlat Code Coverage"
```

---

---

## Auth.API 테스트 명세 (스프린트 #3)

### 프로젝트: `PlatformA.Tests.Auth.API`

**테스트 프레임워크**: xUnit + Moq + Microsoft.AspNetCore.Mvc.Testing  
**대상 프로젝트**: `PlatformA.Auth.API`, `PlatformA.Library`

---

### Mock 전략

| 의존성 | 처리 방법 | 이유 |
|--------|---------|------|
| `RedisManager` | Reflection으로 `_redis`(Mock IConnectionMultiplexer), `_pipeline`(Empty) 주입 | private 생성자 + Singleton 패턴 |
| `IDbContextFactory<DbWebAppContext>` | InMemory 데이터베이스로 교체 | MySQL 특화 DDL(CURRENT_TIMESTAMP(6) 등) 회피 |
| `RedisRateLimiterService` | 상한값 1000으로 재등록 (ScriptEvaluateAsync Mock → 1L 반환) | 테스트에서 Rate Limit 차단 방지 |
| `ScriptEvaluateAsync` (기본) | `RedisResult.Create(1L)` 반환 | Rate Limit 허용 + RefreshToken 유효 시뮬레이션 |

---

### 테스트 클래스 목록

#### 1. `AuthControllerTests` — 통합 테스트 (WebApplicationFactory)

**픽스처**: `IClassFixture<AuthTestWebAppFactory>`

| 테스트 메서드 | HTTP | 경로 | 기대 결과 |
|-------------|------|------|---------|
| `Login_NewUser_ValidCredentials_Returns200_WithTokens` | POST | `/api/auth/login` | 200, `token` + `refreshToken` 필드 존재 |
| `Login_ExistingUser_WrongPassword_Returns401` | POST | `/api/auth/login` | 401 (자동 가입 후 잘못된 비밀번호) |
| `Login_ShortUsername_Returns400` | POST | `/api/auth/login` | 400 (2자 username → DataAnnotation 위반) |
| `Login_ShortPassword_Returns400` | POST | `/api/auth/login` | 400 (5자 password → DataAnnotation 위반) |
| `Refresh_ValidToken_Returns200_WithNewTokens` | POST | `/api/auth/refresh` | 200, `token` + `refreshToken` 필드 존재 |
| `Refresh_MalformedToken_Returns401` | POST | `/api/auth/refresh` | 401 (정수 prefix 없는 토큰 → ParsePlayerId 실패) |
| `Logout_ValidToken_Returns200` | POST | `/api/auth/logout` | 200 |
| `Logout_MalformedToken_Returns401` | POST | `/api/auth/logout` | 401 |

#### 2. `AuthModelValidationTests` — DTO 유닛 테스트 (순수 C#)

`Validator.TryValidateObject()` 사용, HTTP 레이어 없음.

| 테스트 메서드 | 검증 내용 |
|-------------|---------|
| `LoginRequest_ValidData_PassesValidation` | 정상 입력 → 유효성 오류 없음 |
| `LoginRequest_ShortUsername_FailsValidation` | username 2자 → MinLength(3) 위반 |
| `LoginRequest_LongUsername_FailsValidation` | username 21자 → MaxLength(20) 위반 |
| `LoginRequest_InvalidChars_FailsValidation` | username에 공백 포함 → RegularExpression 위반 |
| `LoginRequest_EmptyUsername_FailsValidation` | username 빈 문자열 → Required 위반 |
| `LoginRequest_ShortPassword_FailsValidation` | password 5자 → MinLength(6) 위반 |
| `LoginRequest_EmptyPassword_FailsValidation` | password 빈 문자열 → Required 위반 |
| `RefreshRequest_EmptyToken_FailsValidation` | refreshToken 빈 문자열 → Required 위반 |
| `LogoutRequest_EmptyToken_FailsValidation` | refreshToken 빈 문자열 → Required 위반 |

---

### 실행 명령

```bash
cd PlatformA
dotnet test PlatformA.Tests.Auth.API/PlatformA.Tests.Auth.API.csproj -v normal

# 전체 솔루션 테스트
dotnet test PlatformA.sln
```

---

## 커버리지 기준 (미래)

| 영역 | 최소 목표 |
|------|---------|
| 비즈니스 로직 (Services) | 80% |
| 컨트롤러 | 60% |
| 패킷 직렬화 | 100% |
| Redis 키 로직 | 70% |
