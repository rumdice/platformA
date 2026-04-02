using Microsoft.EntityFrameworkCore;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Library.Helper;
using PlatformA.Utils.API;
using PlatformA.Utils.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.


// sqlite 연결
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=app.db"));

// redis 연결
RedisManager.Instance.Init(Consts.REDIS_CONNECTION_STRING);

// Snowflake 등록 (WorkerId: 1, DatacenterId: 1)
// 나중에 서버 2번을 띄우게 되면 (2, 1)로 바꾸면 됩니다.
builder.Services.AddSingleton(new SnowflakeGenerator(1, 1));

// 백그라운드 서비스(Hosted Service) 등록
builder.Services.AddHostedService<StatSyncsService>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen(); API 서버 문서 비활성화

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()  // 보안상 실제 배포 시에는 "https://rumdice.github.io" 로 특정하는 게 좋습니다.
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    //app.UseSwagger();
    //app.UseSwaggerUI();
}

// app.UseHttpsRedirection(); // 우리서버는 https만 받습니다.(307) -> 임시 주석. 

app.UseCors(); // CORS 미들웨어 적용

app.UseAuthorization();

app.MapControllers();

app.Run();
