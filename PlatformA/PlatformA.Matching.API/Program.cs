using PlatformA.Library.Core;
using PlatformA.Matching.API.Hubs;
using PlatformA.Matching.API.Services;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer(); // Swagger용
builder.Services.AddSwaggerGen();           // Swagger용

builder.Services.AddSingleton<PlatformA.Library.Core.RedisManager>(PlatformA.Library.Core.RedisManager.Instance);

// redis 연결
RedisManager.Instance.Init();


builder.Services.AddSignalR();

// 주식 매도 매수 매칭 엔진 등록
builder.Services.AddSingleton<EngineService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<EngineService>());

// 게임 매칭 엔진 등록
builder.Services.AddSingleton<GameMatchService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<GameMatchService>()); // 백그라운드 워커 등록

// CORS 정책 정의 (문 열어주기)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLevel1", policy =>
    {
        policy.WithOrigins(
                "http://127.0.0.1:5500", // VS Code Live Server 주소 (보통 이거)
                "http://localhost:5500", // 혹시 이걸로 뜰 수도 있음
                "http://localhost:8080"  // 다른 포트라면 그것도 추가
               )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // SignalR은 이게 필수입니다! (쿠키/인증 정보)
    });
});

var app = builder.Build();

// Swagger 설정 (개발 환경에서만)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// CORS 정책 적용 (순서 중요! UseRouting과 UseEndpoints 사이, 보통 맨 위쪽)
app.UseCors("AllowLevel1");

// 정적 파일(HTML) 허용
app.UseStaticFiles();

app.MapControllers();

// Hub 주소 연결
app.MapHub<MatchingHub>("/hubs/matching");

app.Run();