---
description: PlatformA 코딩 패턴 강제 가이드 — 패킷·API·Redis·EF Core·DTO·서비스·헬스체크 전체
globs: ["PlatformA/**/*.cs", "PlatformA/**/*.proto"]
---

# PlatformA 코딩 패턴

## 1. 패킷 (ADR-007: Protobuf)

- 패킷 정의는 `PlatformA.Library/Packets/Proto/packets.proto` 에서만 관리
- C → S 패킷: `C` 접두사 (예: `CMove`), S → C 패킷: `S` 접두사 (예: `SLogin`)
- 새 패킷 추가 절차: `packets.proto` message 추가 → `Packet.oneof` 필드 등록 → `PacketHandler.cs` 핸들러 추가
- **수동 직렬화 절대 금지** — `BitConverter`, `BinaryReader`, `BinaryWriter` 직접 사용 금지
- `[Packet]` 어트리뷰트, `partial struct`, `Size` 상수는 더 이상 사용하지 않는다 (Generator.Lib 제거됨)
- proto3 기본값 주의: 값이 `0`인 enum/int 필드는 wire에 포함되지 않음 (`LOGIN_SUCCESS = 0` 등)

**송신** (`BuildResponsePacket` 헬퍼 사용):
```csharp
byte[] envelopeBytes = envelope.ToByteArray();
ushort size = (ushort)(2 + envelopeBytes.Length);   // 2바이트 헤더
byte[] buf = new byte[size];
BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), size);
envelopeBytes.CopyTo(buf, 2);
```

**수신** (GameSession.OnRecv):
```csharp
ReadOnlySpan<byte> envelopeBytes = span.Slice(2);   // 앞 2바이트(size) 건너뜀
ProtoPacket envelope = ProtoPacket.Parser.ParseFrom(envelopeBytes);
// 파싱 실패: InvalidProtocolBufferException 처리 후 Disconnect()
```

**핸들러** — 게임 상태 수정은 반드시 `room.Push()` 안에서만:
```csharp
[PacketHandler(ProtoPacket.PayloadOneofCase.CMove)]
public static void Handle_C_Move(GameSession session, ProtoPacket packet)
{
    CMove req = packet.CMove;
    GameRoom? room = session.Room;
    if (room == null) return;
    room.Push(() =>
    {
        // ← 이 안에서만 게임 상태 수정
        room.Broadcast(BuildResponsePacket(new ProtoPacket { ... }));
    });
}
```

> **금지**: `room.Push()` 밖에서 게임 상태 수정 — 레이스 컨디션 발생

---

## 2. API 컨트롤러

- `[ApiController]` + `[Route("api/[controller]")]` 클래스 레벨 필수
- JWT 인증 필요 액션: `[Authorize]` 필수
- Rate Limit 필요 액션: `[RedisRateLimit("policyName")]` 사용
- **생성자 DI만 허용** — `new SomeService()` 직접 인스턴스화 절대 금지
- `IDbContextFactory<TContext>` 패턴 사용 (`DbContext` 직접 주입 금지)
- 엔드포인트 추가 전 `AI/API_CONTRACTS.md` 먼저 업데이트

**JWT 추출 패턴** (인증 필요 엔드포인트):
```csharp
string authHeader = Request.Headers["Authorization"].ToString();
if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer"))
    return Unauthorized(new { Message = "토큰이 없습니다." });

int userId = TokenManager.ValidateTokenAndGetUserId(authHeader.Substring(7));
if (userId <= 0)
    return Unauthorized(new { Message = "유효하지 않은 토큰입니다." });
```

**DI 등록** (Program.cs):
```csharp
builder.Services.AddSingleton<ExampleSingletonService>();   // 상태 있음, 전체 앱 수명
builder.Services.AddScoped<ExampleScopedService>();         // 요청당 1개
builder.Services.AddHostedService<ExampleWorkerService>();  // 백그라운드 워커
```

---

## 3. 에러 응답

모든 오류 응답은 `{ Message = "설명" }` 포맷으로 통일:

```csharp
return Unauthorized(new { Message = "설명" });
return BadRequest(new { Message = "설명" });
return NotFound(new { Message = "설명" });
return Ok(new { FieldName = value });
```

- **다른 오류 객체 형식 사용 금지**
- 예외 발생 시 `_logger.LogError(ex, "[서비스명] 설명")` 로깅 필수
- Rate Limit 429: `RedisRateLimitFilter`에서 자동 처리 — 수동 반환 시 `StatusCode(429, "Too many requests")`

---

## 4. Request DTO

```csharp
public class CreateSomethingRequest
{
    [Required(ErrorMessage = "필드는 필수입니다.")]
    [MinLength(3)]
    [MaxLength(100)]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "영문·숫자·밑줄만 허용")]
    public string Name { get; set; } = string.Empty;

    [Range(1, 10000)]
    public int Count { get; set; }
}
```

- 모든 외부 입력 DTO에 DataAnnotation 필수
- 문자열 필드: `[Required]` + `[MinLength]` + `[MaxLength]` 세트
- 정규식 검증 필요 필드(username 등)에 `[RegularExpression]` 적용

---

## 5. Redis 사용

```csharp
// GET
string? value = await RedisManager.Instance.ExecuteAsync(
    db => db.StringGetAsync("key:name"));

// SET with TTL
await RedisManager.Instance.ExecuteAsync(
    db => db.StringSetAsync("key:name", "value", TimeSpan.FromMinutes(10)));

// ZADD
await RedisManager.Instance.ExecuteAsync(
    db => db.SortedSetAddAsync("{ticket:queue}:global", userId, score));

// 서킷 브레이커 처리 (인증 경로)
try
{
    bool exists = await RedisManager.Instance.ExecuteAsync(
        db => db.KeyExistsAsync("key:name"));
}
catch (BrokenCircuitException)
{
    logger.LogError("[Redis] Circuit breaker OPEN");
    return Unauthorized();
}
```

**분산 락**:
```csharp
string lockKey = $"resource:lock:{resourceId}";
string? lockValue = await RedisManager.Instance.LockManager.AcquireLockAsync(
    lockKey,
    expiry: TimeSpan.FromSeconds(30),
    waitTime: TimeSpan.FromSeconds(5),
    retryTime: TimeSpan.FromMilliseconds(100));

if (lockValue == null) return Conflict(new { Message = "리소스가 사용 중입니다." });

try { /* 락 점유 중 작업 */ }
finally { await RedisManager.Instance.LockManager.ReleaseLockAsync(lockKey, lockValue); }
```

**Redis 키 규칙**:
- 새 키는 반드시 `PlatformA.Library/Common/Consts.cs`에 상수로 등록 — 하드코딩 금지
- Cluster 슬롯 고정 필요 시 해시 태그 `{}` 사용
- **TTL 없는 키 원칙적 금지**

---

## 6. EF Core Entity / Migration

**Entity 정의** (`PlatformA.MySqlDB.Lib/DBWebApp/Entities/`):
```csharp
public class NewEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int PlayerId { get; set; }
    public Player Player { get; set; } = null!;
}
```

**DbContext 등록** (`DbWebAppContext.cs`):
```csharp
public virtual DbSet<NewEntity> NewEntities { get; set; }

// OnModelCreating
modelBuilder.Entity<NewEntity>(entity =>
{
    entity.ToTable("new_entities");          // 명시적 snake_case 테이블명
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
    entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
    entity.HasIndex(e => e.PlayerId);
    entity.HasOne(e => e.Player).WithMany()
        .HasForeignKey(e => e.PlayerId).OnDelete(DeleteBehavior.Cascade);
});
```

**Migration 생성**:
```bash
cd PlatformA/PlatformA.MySqlDB.Lib
dotnet ef migrations add <이름> --context DbWebAppContext --output-dir Migrations/WebApp
dotnet ef migrations add <이름> --context DbLogAppContext --output-dir Migrations/LogApp
```

| Context | 용도 | Migration 경로 |
|---------|------|---------------|
| `DbWebAppContext` | 게임 플레이어/아이템/매칭 | `Migrations/WebApp` |
| `DbLogAppContext` | 접속 로그 | `Migrations/LogApp` |

- 테이블명·컬럼명: `snake_case`
- Migration 이름: PascalCase 동사+명사 (예: `AddRatingColumn`, `CreateMatchRecordsTable`)
- **절대 금지**: `ExecuteSqlRaw()`, `ALTER TABLE` 등 직접 SQL 실행
- 적용은 `db-migrator` 에이전트를 통해 Up()/Down() 안전성 검증 후 실행

---

## 7. 백그라운드 서비스

```csharp
public class ExampleWorkerService : BackgroundService
{
    private readonly ILogger<ExampleWorkerService> _logger;
    private readonly ExampleService _service;

    public ExampleWorkerService(ILogger<ExampleWorkerService> logger, ExampleService service)
    {
        _logger = logger;
        _service = service;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await _service.DoPeriodicWorkAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "[Worker] 주기 작업 실패"); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
```

- `Program.cs`에 `builder.Services.AddHostedService<ExampleWorkerService>()` 등록
- 모든 주기 작업은 `try-catch`로 감싸 워커 크래시 방지
- `stoppingToken` 전파 필수

---

## 8. Health Check

```csharp
// Program.cs 등록
builder.Services.AddHealthChecks()
    .AddRedis(Consts.REDIS_CONNECTION_STRING, name: "redis", tags: ["readiness"])
    .AddCheck<CustomHealthCheck>("custom-check", tags: ["readiness"]);

// /healthz (liveness) — 외부 의존성 체크 없음, 항상 200
app.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false });

// /readyz (readiness) — Redis + DB 포함, 503 가능
app.MapHealthChecks("/readyz", new HealthCheckOptions
{
    Predicate = h => h.Tags.Contains("readiness"),
    ResponseWriter = WriteJsonResponse
});
```

```csharp
// IHealthCheck 구현
public class CustomHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct)
    {
        try { /* 검증 로직 */; return HealthCheckResult.Healthy(); }
        catch (Exception ex) { return HealthCheckResult.Unhealthy(ex.Message); }
    }
}
```

- Liveness(`/healthz`): 프로세스 생존 여부만 — `Predicate = _ => false`
- Readiness(`/readyz`): Redis + DB 연결 — `tags: ["readiness"]`로 구분
