using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

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
