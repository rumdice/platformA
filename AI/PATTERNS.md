# PATTERNS — 코딩 패턴 강제 가이드

> 이 패턴에서 벗어나려면 사용자 승인 필요.
> 모든 예시는 실제 코드베이스에서 추출됨.

---

## 1. 패킷 추가 패턴 (Game Server)

새 패킷을 추가할 때 반드시 이 3단계를 순서대로 수행.

### Step 1: PacketID 등록
**파일**: `PlatformA.Library/Packets/Packet.cs`

```csharp
public enum PacketID : ushort
{
    C_Move = 1,
    S_Move = 2,
    C_Login = 3,
    S_Login = 4,
    C_EnterRoom = 5,
    S_EnterRoom = 6,
    // 여기에 추가
    C_Chat = 7,
    S_Chat = 8,
}
```

### Step 2: 패킷 구조체 정의
**파일**: `PlatformA.Library/Packets/ChatPacket.cs` (새 파일)

```csharp
using PlatformA.Generator.Lib;

[Packet]  // Source Generator가 Serialize/Deserialize 자동 생성
public partial struct C_ChatPacket
{
    public int RoomId;
    public int SenderId;
    // 가변 길이 필드는 현재 미지원 — 고정 크기 필드만
    
    public const ushort Size = 8;  // 페이로드 바이트 크기 (수동 계산 필수)
}

[Packet]
public partial struct S_ChatPacket
{
    public int RoomId;
    public int SenderId;
    
    public const ushort Size = 8;
}
```

> **Size 계산**: int = 4 bytes, float = 4 bytes, ushort = 2 bytes, byte = 1 byte

### Step 3: 핸들러 등록
**파일**: `PlatformA.Game.Server/Packet/PacketHandler.cs`

```csharp
[PacketHandler((ushort)PacketID.C_Chat)]
public static void Handle_C_Chat(GameSession session, ReadOnlySpan<byte> payload)
{
    // 1. 역직렬화 (Source Generator가 생성한 메서드)
    C_ChatPacket req = new C_ChatPacket();
    req.Deserialize(payload);
    
    // 2. GameRoom을 통해 처리 (스레드 안전)
    GameRoom? room = session.Room;
    if (room == null) return;
    
    room.Push(() =>
    {
        // 3. 응답 패킷 조립
        S_ChatPacket res = new S_ChatPacket()
        {
            RoomId = req.RoomId,
            SenderId = session.SessionId,
        };
        
        ushort totalSize = (ushort)(4 + S_ChatPacket.Size);
        byte[] sendBuffer = new byte[totalSize];
        Span<byte> span = sendBuffer.AsSpan();
        
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(0, 2), totalSize);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(2, 2), (ushort)PacketID.S_Chat);
        res.Serialize(span.Slice(4));
        
        // 4. 브로드캐스트 또는 단일 전송
        room.Broadcast(sendBuffer);          // 방 전체
        // session.SendAsync(sendBuffer);   // 특정 세션만
    });
}
```

**금지**: `room.Push()` 밖에서 게임 상태 수정 — 레이스 컨디션 발생

---

## 2. API 엔드포인트 추가 패턴

### 컨트롤러 구조
**참조**: `PlatformA.Auth.API/Controllers/AuthController.cs`

```csharp
[ApiController]
[Route("api/[controller]")]
public class ExampleController : ControllerBase
{
    private readonly ExampleService _service;
    private readonly ILogger<ExampleController> _logger;
    
    // 생성자 DI — new 키워드로 직접 생성 금지
    public ExampleController(ExampleService service, ILogger<ExampleController> logger)
    {
        _service = service;
        _logger = logger;
    }
    
    [RedisRateLimit("policyName")]   // Rate Limit 필요 시
    [HttpPost("action")]
    public async Task<IActionResult> DoAction([FromBody] RequestDto request)
    {
        // JWT에서 userId 추출 (인증 필요 시)
        string authHeader = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer"))
            return Unauthorized(new { Message = "토큰이 없습니다." });
        
        int userId = TokenManager.ValidateTokenAndGetUserId(authHeader.Substring(7));
        if (userId <= 0)
            return Unauthorized(new { Message = "유효하지 않은 토큰입니다." });
        
        try
        {
            var result = await _service.DoSomethingAsync(userId);
            if (result == null)
                return NotFound(new { Message = "리소스를 찾을 수 없습니다." });
            
            _logger.LogInformation("[Example] 성공. UserId: {UserId}", userId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Example] 예외 발생. UserId: {UserId}", userId);
            return BadRequest(ex.Message);
        }
    }
}
```

### DI 등록 (Program.cs)
```csharp
// Singleton: 상태 있음, 전체 앱 수명
builder.Services.AddSingleton<ExampleSingletonService>();

// Scoped: 요청당 1개 인스턴스 (컨트롤러에 주로 사용)
builder.Services.AddScoped<ExampleScopedService>();

// Hosted Service: 백그라운드 워커
builder.Services.AddHostedService<ExampleWorkerService>();
```

### API 추가 전 필수
`AI/API_CONTRACTS.md` 먼저 업데이트 → 구현 → PR

---

## 3. Redis 사용 패턴

### 기본 명령 (Polly 래핑 필수)
```csharp
// GET
string? value = await RedisManager.Instance.ExecuteAsync(
    db => db.StringGetAsync("key:name"));

// SET with TTL
await RedisManager.Instance.ExecuteAsync(
    db => db.StringSetAsync("key:name", "value", TimeSpan.FromMinutes(10)));

// ZADD (ZSet)
await RedisManager.Instance.ExecuteAsync(
    db => db.SortedSetAddAsync("{ticket:queue}:global", userId, score));

// 서킷 브레이커 열린 경우 처리
try
{
    bool exists = await RedisManager.Instance.ExecuteAsync(
        db => db.KeyExistsAsync("key:name"));
}
catch (BrokenCircuitException)
{
    // 서킷 오픈: 페일-세이프 처리
    logger.LogError("[Redis] Circuit breaker OPEN");
    return Unauthorized();
}
```

### 분산 락
```csharp
string lockKey = $"resource:lock:{resourceId}";
string? lockValue = await RedisManager.Instance.LockManager.AcquireLockAsync(
    lockKey,
    expiry: TimeSpan.FromSeconds(30),
    waitTime: TimeSpan.FromSeconds(5),
    retryTime: TimeSpan.FromMilliseconds(100));

if (lockValue == null)
{
    // 락 획득 실패 (이미 다른 인스턴스가 점유)
    return Conflict(new { Message = "리소스가 사용 중입니다." });
}

try
{
    // 락 점유 중 작업 수행
}
finally
{
    await RedisManager.Instance.LockManager.ReleaseLockAsync(lockKey, lockValue);
}
```

### Redis 키 네이밍 규칙
- 새 키는 반드시 `Consts.cs`에 상수로 등록
- Cluster 슬롯 고정 필요 시 해시 태그 `{}` 사용
- TTL 없는 키는 원칙적으로 사용 금지

---

## 4. EF Core Entity 추가 패턴

### Step 1: Entity 클래스 정의
**위치**: `PlatformA.MySqlDB.Lib/DBWebApp/Entities/`

```csharp
public class NewEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    
    // 연관 엔티티 (필요 시)
    public int PlayerId { get; set; }
    public Player Player { get; set; } = null!;
}
```

### Step 2: DbContext에 DbSet 추가
**파일**: `PlatformA.MySqlDB.Lib/DBWebApp/Entities/DbWebAppContext.cs`

```csharp
public virtual DbSet<NewEntity> NewEntities { get; set; }
```

### Step 3: OnModelCreating 설정
```csharp
modelBuilder.Entity<NewEntity>(entity =>
{
    entity.ToTable("new_entities");        // 명시적 테이블명 (snake_case)
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Name)
        .IsRequired()
        .HasMaxLength(100);
    entity.Property(e => e.CreatedAt)
        .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
    
    // 인덱스 (자주 검색하는 컬럼)
    entity.HasIndex(e => e.PlayerId);
    
    // FK 관계
    entity.HasOne(e => e.Player)
        .WithMany()
        .HasForeignKey(e => e.PlayerId)
        .OnDelete(DeleteBehavior.Cascade);
});
```

### Step 4: Migration 생성 및 적용
```bash
cd /home/user/platformA/PlatformA/PlatformA.MySqlDB.Lib
dotnet ef migrations add Add_NewEntity \
  --context DbWebAppContext \
  --output-dir Migrations/WebApp
dotnet ef database update --context DbWebAppContext
```

---

## 5. 에러 응답 포맷

```csharp
// 401 Unauthorized
return Unauthorized(new { Message = "설명" });

// 400 Bad Request  
return BadRequest(new { Message = "설명" });

// 404 Not Found
return NotFound(new { Message = "설명" });

// 200 OK (성공)
return Ok(new ResponseDto { ... });
return Ok(new { FieldName = value });

// 429 Rate Limit (RedisRateLimitFilter에서 자동 처리)
// 수동으로 반환 필요한 경우:
return StatusCode(429, "Too many requests");
```

---

## 6. Request DTO 정의 패턴
**참조**: `PlatformA.Auth.API/Models/Auth.cs`

```csharp
public class CreateSomethingRequest
{
    [Required(ErrorMessage = "필드는 필수입니다.")]
    [MinLength(3)]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Range(1, 10000)]
    public int Count { get; set; }
}
```

---

## 7. 백그라운드 서비스 패턴
**참조**: `PlatformA.Ticketing.API/Services/QueueWorkerService.cs`

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
            try
            {
                await _service.DoPeriodicWorkAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Worker] 주기 작업 실패");
            }
            
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}

// Program.cs에서 등록
builder.Services.AddHostedService<ExampleWorkerService>();
```

---

## 8. Health Check 추가 패턴

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddRedis(Consts.REDIS_CONNECTION_STRING, name: "redis", tags: ["readiness"])
    .AddCheck<CustomHealthCheck>("custom-check", tags: ["readiness"]);

// 커스텀 헬스체크
public class CustomHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct)
    {
        try
        {
            // 검증 로직
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}
```
