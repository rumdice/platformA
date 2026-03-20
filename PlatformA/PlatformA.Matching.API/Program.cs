using PlatformA.Library.Core;
using PlatformA.Matching.API.Hubs;
using PlatformA.Matching.API.Services;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer(); // Swagger용
builder.Services.AddSwaggerGen();           // Swagger용

// redis 연결
RedisManager.Instance.Init();


// 1. SignalR 서비스 등록
builder.Services.AddSignalR();

// 🔥 [핵심] 매칭 엔진 등록
// 1. Singleton으로 등록 (전역 유일 인스턴스)
builder.Services.AddSingleton<EngineService>();
// 2. HostedService로 등록 (서버 켜질 때 ExecuteAsync 실행)
builder.Services.AddHostedService(provider => provider.GetRequiredService<EngineService>());

// 게임 매칭 서비스 등록
builder.Services.AddSingleton<GameMatchService>();

// 🔥 [추가] 1. CORS 정책 정의 (문 열어주기)
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

// 🔥 [추가] 2. CORS 정책 적용 (순서 중요! UseRouting과 UseEndpoints 사이, 보통 맨 위쪽)
app.UseCors("AllowLevel1");

// 2. 정적 파일(HTML) 허용
app.UseStaticFiles();

app.MapControllers();

// 3. Hub 주소 연결
app.MapHub<MatchingHub>("/hubs/matching");

app.Run();