# 요구사항 명세: ModernizeNet10Stack

작성일: 2026-05-30
브랜치: 2026-05-30_ModernizeNet10Stack
소스: plan mode — ~/.claude/plans/8-0-10-0-imperative-shamir.md

## 요구사항 요약

.NET 8→10 업그레이드 완료 이후 빌드 실패를 해소하고, Swashbuckle을 .NET 10 공식 OpenAPI
스택(Microsoft.AspNetCore.OpenApi + Scalar)으로 전환하며, C# primary constructors와
collection expressions를 코드베이스에 적용한다.

## 상세 요구사항

### Phase 1 — 빌드 수정

1. `dotnet clean PlatformA.sln && dotnet build` 실행 — 스테일 캐시 해소
2. clean 후에도 실패하면:
   - `PlatformA.Library.csproj`의 Grpc.Tools를 최신 stable로 업그레이드
   - Google.Protobuf를 Grpc.Tools와 동일 major.minor로 맞춤
   - `PlatformA.Game.DummyClient.csproj`의 Google.Protobuf도 동일하게 맞춤

### Phase 2 — OpenAPI 현대화

3. `PlatformA.Library.csproj`에서 `Swashbuckle.AspNetCore 6.6.2` 제거
4. Auth / Ticketing / Matching API csproj에서 Swashbuckle 제거 후
   `Microsoft.AspNetCore.OpenApi 10.0.x` + `Scalar.AspNetCore` 추가
5. Utils.API csproj에서 `Microsoft.AspNetCore.OpenApi` 버전만 9.0.0 → 10.0.x 업그레이드
   (OpenAPI 기능 자체는 비활성 상태 유지)
6. Auth / Ticketing / Matching API `Program.cs`에서:
   - `AddEndpointsApiExplorer()` + `AddSwaggerGen(...)` 제거
   - `app.UseSwagger()` + `app.UseSwaggerUI()` 제거
   - `builder.Services.AddOpenApi(options => { options.AddDocumentTransformer<BearerSecuritySchemeTransformer>(); })` 추가
   - `if (IsDevelopment) { app.MapOpenApi(); app.MapScalarApiReference(); }` 추가
7. 각 API 프로젝트에 `OpenApi/BearerSecuritySchemeTransformer.cs` 신규 생성:
   - `IOpenApiDocumentTransformer` 구현
   - `IAuthenticationSchemeProvider` 의존성 없이 SecurityScheme 직접 등록
   - 모든 Operations에 Bearer 보안 요구사항 추가 (Array.Empty → [])

### Phase 3 — C# 언어 현대화

8. `Directory.Build.props`에 `<LangVersion>latest</LangVersion>` 추가
9. Primary Constructors 적용 대상 (필드 → 파라미터 직접 사용):
   - `Auth.API/Services/RefreshTokenService.cs`
   - `Auth.API/Services/PlayerService.cs`
   - `Auth.API/Controllers/AuthController.cs`
   - `Auth.API/Filters/RedisRateLimitFilter.cs`
   - `Ticketing.API/Services/QueueService.cs`
   - `Matching.API/Services/EngineService.cs`
10. Collection Expressions 적용:
    - `PlatformA.Library/Network/RedisManager.cs` — `CommandMap.Create(new HashSet<string> {...})` → `[]`

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|------|---------|
| `PlatformA.Library/PlatformA.Library.csproj` | 패키지 수정 (Grpc.Tools 업그레이드, Swashbuckle 제거) |
| `PlatformA.Game.DummyClient/PlatformA.Game.DummyClient.csproj` | Google.Protobuf 버전 맞춤 |
| `PlatformA.Auth.API/PlatformA.Auth.API.csproj` | 패키지 교체 |
| `PlatformA.Ticketing.API/PlatformA.Ticketing.API.csproj` | 패키지 교체 |
| `PlatformA.Matching.API/PlatformA.Matching.API.csproj` | 패키지 교체 |
| `PlatformA.Utils.API/PlatformA.Utils.API.csproj` | 패키지 버전 업 |
| `PlatformA.Auth.API/Program.cs` | Swagger → OpenApi+Scalar |
| `PlatformA.Ticketing.API/Program.cs` | Swagger → OpenApi+Scalar |
| `PlatformA.Matching.API/Program.cs` | Swagger → OpenApi+Scalar |
| `PlatformA.Auth.API/OpenApi/BearerSecuritySchemeTransformer.cs` | 신규 생성 |
| `PlatformA.Ticketing.API/OpenApi/BearerSecuritySchemeTransformer.cs` | 신규 생성 |
| `PlatformA.Matching.API/OpenApi/BearerSecuritySchemeTransformer.cs` | 신규 생성 |
| `PlatformA/Directory.Build.props` | LangVersion 추가 |
| `Auth.API/Services/RefreshTokenService.cs` | Primary Constructor |
| `Auth.API/Services/PlayerService.cs` | Primary Constructor |
| `Auth.API/Controllers/AuthController.cs` | Primary Constructor |
| `Auth.API/Filters/RedisRateLimitFilter.cs` | Primary Constructor |
| `Ticketing.API/Services/QueueService.cs` | Primary Constructor |
| `Matching.API/Services/EngineService.cs` | Primary Constructor |
| `PlatformA.Library/Network/RedisManager.cs` | Collection Expression (CommandMap) |

**테스트 코드 변경 없음** — 모든 테스트 팩토리가 `Testing` 환경에서 실행되어 OpenAPI 블록을 건너뜀.

## 제약 및 주의사항

- **ADR-007 (Protobuf)**: Grpc.Tools 버전 업 시 `obj/` 내 proto 생성 코드가 재생성됨.
  `GrpcServices="None"` 유지 — gRPC 서비스 코드 생성 없이 메시지 클래스만 생성. 호환성 위험 낮음.
- **RedisManager 변경 금지**: 테스트 팩토리가 `FieldInfo.GetField("_redis", NonPublic|Instance)`로
  Reflection 주입. 필드 이름·접근성 변경 시 테스트 런타임 오류 발생. Primary Constructor 적용 제외.
- **DbContext 변경 금지**: EF Core DbContext는 `base(options)` 호출 필요.
- **EF Core 9.x 유지**: Pomelo 10.x 미출시. `net10.0` 환경에서 EF Core 9.x 패키지 정상 동작.
- **Utils.API OpenAPI 비활성 유지**: `AddOpenApi()` 호출 추가하지 않음.
- **Grpc.Tools + Google.Protobuf**: 반드시 같은 gRPC 릴리스 트레인 버전으로 맞춤.

## 구현 접근 방향

1. 빌드 clean → rebuild로 스테일 캐시 해소 시도 (가장 먼저)
2. 빌드 실패 지속 시 Grpc.Tools / Google.Protobuf 버전 업 (NuGet 최신 stable 확인)
3. csproj 패키지 변경 (Swashbuckle 제거, OpenApi/Scalar 추가) → dotnet restore
4. Program.cs 3개 + BearerSecuritySchemeTransformer 3개 신규 추가
5. Directory.Build.props LangVersion 추가
6. Primary constructors 6개 클래스 적용
7. RedisManager collection expression 적용
8. dotnet build → dotnet test 검증

## DESIGN_REVIEW 결과

| ADR | 관련 여부 | 충돌/참고 사항 |
|-----|---------|--------------|
| ADR-007: Protobuf 패킷 마이그레이션 | 관련 있음 | Grpc.Tools 업 시 proto 재컴파일. GrpcServices=None으로 위험 낮음 ✅ |
| ADR-001~006 | 관련 없음 | Redis·설정·매칭·패킷 결정과 무관 |

**판정: ✅ 기존 ADR 준수 — 신규 ADR 불필요**

OpenAPI 라이브러리는 개발 보조 도구 수준 전환이며 ADR 체계 대상이 아님.

## 검증 기준

- `dotnet build PlatformA.sln` — 경고 포함 오류 0개
- `dotnet test PlatformA.sln` — 실패 0개 (기존 테스트 전부 통과)
- `https://localhost:7001/scalar/v1` — Scalar UI 접근, Bearer 토큰 입력란 확인
- `https://localhost:7001/openapi/v1.json` — OpenAPI 스펙 JSON 반환 확인
- Auth/Ticketing/Matching API에서 `/api/...` 엔드포인트 정상 응답 확인 (기능 회귀 없음)
