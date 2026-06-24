# 요구사항 명세: AddUtilsHealthcheck

작성일: 2026-06-25
브랜치: 2026-06-25_AddUtilsHealthcheck
소스: .claude/plan/2026-06-25_UtilsApiHealthcheck.md

## 요구사항 요약

Utils.API에 `/healthz`(liveness)와 `/readyz`(Redis readiness) 헬스체크 엔드포인트를 추가한다.
Auth.API, Ticketing.API, Matching.API와 동일한 패턴을 따르며, Utils.API는 SQLite를 사용하므로 DB 체크는 제외한다.

## 상세 요구사항

1. **NuGet 패키지 추가** (`PlatformA.Utils.API.csproj`)
   - `AspNetCore.HealthChecks.Redis` Version="9.0.0" (Auth.API와 동일 버전)

2. **Program.cs 수정** — 서비스 등록 섹션 끝에 추가
   ```csharp
   builder.Services.AddHealthChecks()
       .AddRedis(Consts.REDIS_CONNECTION_STRING, name: "redis", tags: ["readiness"]);
   ```

3. **Program.cs 수정** — `app.MapControllers()` 이후에 엔드포인트 추가
   ```csharp
   app.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false });
   app.MapHealthChecks("/readyz", new HealthCheckOptions
   {
       Predicate = h => h.Tags.Contains("readiness"),
       ResponseWriter = WriteJsonResponse
   });
   ```

4. **WriteJsonResponse 헬퍼** — `app.Run()` 아래에 추가
   ```csharp
   static Task WriteJsonResponse(HttpContext ctx, HealthReport report)
   {
       ctx.Response.ContentType = "application/json; charset=utf-8";
       var result = JsonSerializer.Serialize(new
       {
           status = report.Status.ToString(),
           duration = report.TotalDuration.TotalMilliseconds,
           checks = report.Entries.ToDictionary(
               e => e.Key,
               e => new { status = e.Value.Status.ToString(), description = e.Value.Description })
       });
       return ctx.Response.WriteAsync(result);
   }
   ```

5. **using 추가** — `Microsoft.AspNetCore.Diagnostics.HealthChecks`, `Microsoft.Extensions.Diagnostics.HealthChecks`, `System.Text.Json`

## 영향 범위 (예상)

| 파일 | 변경 내용 |
|------|---------|
| `PlatformA.Utils.API/PlatformA.Utils.API.csproj` | AspNetCore.HealthChecks.Redis 패키지 추가 |
| `PlatformA.Utils.API/Program.cs` | AddHealthChecks 등록, MapHealthChecks 엔드포인트, WriteJsonResponse 헬퍼 |

## 제약 및 주의사항

- `Consts.REDIS_CONNECTION_STRING` 사용 — 하드코딩 금지
- DB(SQLite) 체크는 추가하지 않는다 — Utils.API에서 DB는 부가 기능(통계 저장)이므로 readiness와 무관
- `public partial class Program { }` 줄은 Program.cs 마지막에 유지 (테스트 팩토리용)
- 기존 `app.UseCors()` 미들웨어 위치 변경 없음

## 구현 접근 방향

Auth.API `Program.cs`의 헬스체크 섹션(lines 62-116)을 참조하여 그대로 복사 + DB 체크 부분만 제외한다.
`WriteJsonResponse`는 Auth.API와 완전히 동일한 구현을 사용한다.

## 검증 기준

- `dotnet build PlatformA.sln` 오류 0개
- `dotnet test PlatformA.sln` 전체 통과
- Utils.API 기동 후:
  - `GET /healthz` → 200 OK `{"status":"Healthy",...}`
  - `GET /readyz` → 200 OK (Redis 정상 시) 또는 503 (Redis 장애 시)
