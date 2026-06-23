using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using PlatformA.Game.Lobby.Hubs;
using PlatformA.Game.Lobby.Services;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ───────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// ── Redis ─────────────────────────────────────────────────────
builder.Services.AddSingleton<RedisManager>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<RedisManager>>();
    RedisManager.Instance.Init(Consts.REDIS_CONNECTION_STRING, logger);
    return RedisManager.Instance;
});

// ── JWT 인증 ───────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = Consts.JWT_ISSUER,
            ValidAudience = Consts.JWT_AUDIENCE,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Consts.SECRET_KEY)),
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                string? token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    context.Request.Path.StartsWithSegments("/hubs/lobby"))
                    context.Token = token;
                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization();

// ── SignalR ────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── HttpClient ─────────────────────────────────────────────────
builder.Services.AddHttpClient("MatchingAPI", client =>
{
    string url = Environment.GetEnvironmentVariable("MATCHING_API_URL") ?? "http://localhost:7002";
    client.BaseAddress = new Uri(url);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ── 서비스 ────────────────────────────────────────────────────
builder.Services.AddSingleton<LobbyPresenceService>();
builder.Services.AddHostedService<MatchNotificationService>();

// ── Health Check ───────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddRedis(Consts.REDIS_CONNECTION_STRING, name: "redis", tags: ["readiness"]);

var app = builder.Build();

// Redis 초기화 트리거
app.Services.GetRequiredService<RedisManager>();

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<LobbyHub>("/hubs/lobby");
app.MapGet("/", () => Results.Ok(new { service = "PlatformA.Game.Lobby", status = "running" }));

app.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/readyz", new HealthCheckOptions
{
    Predicate = h => h.Tags.Contains("readiness"),
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() }),
        }));
    },
});

app.Run();
