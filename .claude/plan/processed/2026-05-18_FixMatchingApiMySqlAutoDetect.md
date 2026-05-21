# Plan: PR #43 CI 실패 수정 — Matching.API MySQL AutoDetect 제거

## Context

PR #43 (2026-05-18_AISDLCEnhancements) CI의 Test 단계에서 `PlatformA.Tests.Matching.API` 8개 테스트 전부 실패.

**에러**: `MySqlConnector.MySqlException : Unable to connect to any of the specified MySQL hosts`  
**발생 위치**: `Microsoft.EntityFrameworkCore.ServerVersion.AutoDetect(String connectionString)`

### 근본 원인

`Matching.API/Program.cs`의 `AddDbContextFactory`가 `ServerVersion.AutoDetect(connectionString)`을 사용:

```csharp
builder.Services.AddDbContextFactory<DbWebAppContext>(options =>
{
    options.UseMySql(
        Consts.MYSQL_WEBAPP_CONNECTION,
        ServerVersion.AutoDetect(Consts.MYSQL_WEBAPP_CONNECTION));  // ← 문제
    options.UseSnakeCaseNamingConvention();
});
```

`ServerVersion.AutoDetect`는 이 options 람다가 처음 실행될 때(= 첫 번째 `IDbContextFactory<DbWebAppContext>` 접근 시) MySQL에 즉시 TCP 연결을 시도한다. CI(Ubuntu) 환경에는 MySQL이 없어서 실패.

`MatchingTestWebAppFactory`가 `IDbContextFactory<DbWebAppContext>`와 `DbContextOptions<DbWebAppContext>`를 InMemory로 교체하려 하지만, EF Core의 `TryAdd` 패턴으로 인해 교체가 완전하지 않아 MySQL 옵션 람다가 여전히 실행된다.

### 로컬 통과 이유

로컬에서는 MySQL Docker가 `localhost:3306`에서 실행 중이므로 `AutoDetect`가 성공.

---

## 수정 내용

### 파일 1: `PlatformA/PlatformA.Matching.API/Program.cs`

`ServerVersion.AutoDetect` → 고정 버전으로 교체 (MySQL 연결 시도 자체를 제거):

```csharp
// 변경 전
options.UseMySql(
    Consts.MYSQL_WEBAPP_CONNECTION,
    ServerVersion.AutoDetect(Consts.MYSQL_WEBAPP_CONNECTION));

// 변경 후
options.UseMySql(
    Consts.MYSQL_WEBAPP_CONNECTION,
    new MySqlServerVersion(new Version(8, 0, 0)));
```

> **이유**: `new MySqlServerVersion(8, 0, 0)`은 "MySQL 8.0 이상"을 의미하며, Pomelo 커넥터가 버전 감지 없이 동작한다. 로컬 Docker MySQL도 8.0이므로 운영에 영향 없음.

### 파일 2: `PlatformA/PlatformA.Tests.Matching.API/Helpers/MatchingTestWebAppFactory.cs`

`toRemove` 목록에서 잘못된 타입 제거 + 방어적 비제네릭 옵션 추가:

```csharp
// 변경 전
var toRemove = services
    .Where(d => d.ServiceType == typeof(IDbContextFactory<DbWebAppContext>)
             || d.ServiceType == typeof(DbWebAppContext)          // ← 효과 없음
             || d.ServiceType == typeof(DbContextOptions<DbWebAppContext>))
    .ToList();

// 변경 후
var toRemove = services
    .Where(d => d.ServiceType == typeof(IDbContextFactory<DbWebAppContext>)
             || d.ServiceType == typeof(DbContextOptions<DbWebAppContext>)
             || d.ServiceType == typeof(DbContextOptions))        // ← 비제네릭도 제거
    .ToList();
```

---

## 파일 경로

| 파일 | 변경 위치 |
|------|---------|
| `PlatformA/PlatformA.Matching.API/Program.cs` | L57 |
| `PlatformA/PlatformA.Tests.Matching.API/Helpers/MatchingTestWebAppFactory.cs` | L91 |

---

## 검증

```bash
# 1. 빌드
cd PlatformA && dotnet build PlatformA.sln

# 2. Matching.API 테스트만 먼저 확인
dotnet test PlatformA.Tests.Matching.API/PlatformA.Tests.Matching.API.csproj -q
# 기대: Passed: 8

# 3. 전체 테스트
dotnet test PlatformA.sln -q
# 기대: 113개 모두 통과

# 4. push → CI 재실행 확인
```

---

## 추가 메모: `/qa-failure` 스킬 검증

이 수정 작업과 별개로, `/qa-failure` 스킬 자체는 정상 구현됨 (GitHub CLI 활용, BUILD/FORMAT/TEST 분류, fixable_by_ai 판정). 단, CI 실패 분석은 **수동으로 `/qa-failure` 를 실행해야** 동작하며, `/done` 스킬과 자동 연동은 없음 (설계 의도대로).
