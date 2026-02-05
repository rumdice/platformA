using PlatformA.Matching.API.Services;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer(); // Swagger용
builder.Services.AddSwaggerGen();           // Swagger용

// 🔥 [핵심] 매칭 엔진 등록
// 1. Singleton으로 등록 (전역 유일 인스턴스)
builder.Services.AddSingleton<EngineService>();
// 2. HostedService로 등록 (서버 켜질 때 ExecuteAsync 실행)
builder.Services.AddHostedService(provider => provider.GetRequiredService<EngineService>());

var app = builder.Build();

// Swagger 설정 (개발 환경에서만)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();