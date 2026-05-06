---
description: ASP.NET Core 컨트롤러 코딩 규칙 — DI, JWT, Rate Limit, 응답 형식 강제
globs: ["PlatformA/**/Controllers/**"]
---

# API 컨트롤러 코딩 규칙

## 필수 어트리뷰트
- `[ApiController]` + `[Route("api/[controller]")]` 클래스 레벨 필수
- JWT 인증이 필요한 액션에는 `[Authorize]` 필수
- Rate Limit이 필요한 액션에는 `[RedisRateLimit("policyName")]` 사용 (커스텀 속성)

## 의존성 주입
- 생성자 DI만 허용 — `new SomeService()` 직접 인스턴스화 절대 금지
- `IDbContextFactory<TContext>` 패턴 사용 (`DbContext` 직접 주입 금지)

## 엔드포인트 추가 절차
1. `AI/API_CONTRACTS.md`에 명세 먼저 작성
2. 컨트롤러 구현
3. 테스트 작성

## 응답 형식 통일
- 성공: `Ok(new { ... })` 또는 `CreatedAtAction(...)`
- 오류: `new { Message = "설명" }` 형식으로 통일 — 다른 오류 객체 형식 사용 금지
- 400 오류: `BadRequest(new { Message = "..." })`
- 401/403: `Unauthorized()` 또는 `Forbid()`
- 404: `NotFound(new { Message = "..." })`

## 기존 패턴 참조
- Auth.API 컨트롤러: `PlatformA.Auth.API/Controllers/AuthController.cs`
- Utils.API 컨트롤러: `PlatformA.Utils.API/Controllers/UtilController.cs`
