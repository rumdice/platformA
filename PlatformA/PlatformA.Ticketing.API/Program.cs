using StackExchange.Redis;
using RedLockNet.SERedis; // 추가
using RedLockNet.SERedis.Configuration; // 추가

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// redis 연결
// docker run --name my-redis -p 6379:6379 -d redis 로컬 환경에서 설치 필요.
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse("localhost:6379", true);
    return ConnectionMultiplexer.Connect(configuration);
});

// 2. 🔥 [추가] RedLock 팩토리 등록
builder.Services.AddSingleton<RedLockFactory>(sp =>
{
    var redis = sp.GetRequiredService<IConnectionMultiplexer>();
    // Redis 연결 객체를 리스트로 전달 (Multiplexer가 여러 개일 수도 있어서)
    return RedLockFactory.Create(new List<RedLockEndPoint> { new RedLockEndPoint(redis.GetEndPoints()[0]) });
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 앱 종료 시 팩토리 정리 (Memory Leak 방지)
app.Lifetime.ApplicationStopping.Register(() =>
{
    var factory = app.Services.GetService<RedLockFactory>();
    factory?.Dispose();
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
