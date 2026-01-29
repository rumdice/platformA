using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlatformA.Library.Helper;
using PlatformA.Utils.API;
using StackExchange.Redis;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Snowflake 등록 (WorkerId: 1, DatacenterId: 1)
// 나중에 서버 2번을 띄우게 되면 (2, 1)로 바꾸면 됩니다.
builder.Services.AddSingleton(new SnowflakeGenerator(1, 1));

// sqlite 연결
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=app.db"));

// redis 연결
// TODO: docker run --name my-redis -p 6379:6379 -d redis 로컬 환경에서 설치 필요.
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse("localhost:6379", true);
    return ConnectionMultiplexer.Connect(configuration);
});

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
