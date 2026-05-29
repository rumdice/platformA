# 요구사항 명세 — .NET 8/9 → 10 전체 업그레이드

**작성일**: 2026-05-29  
**스프린트**: #30  
**브랜치**: 2026-05-29_DotNet10Upgrade  
**상태**: 처리 완료 (소급 복구)

---

## 1. 배경 및 목적

`global.json`이 SDK `9.0.100`을 요구하나 로컬에 `10.0.300`만 설치되어 빌드 실패 중이다.
원본 계획(`binary-hatching-kernighan.md`)을 검토·수정하여 전체 TFM을 `net10.0`으로 통일한다.

패키지 가용성 확인 결과:
- `Pomelo.EntityFrameworkCore.MySql` 최신: **9.0.0** (10.x 미출시) → EF Core 9.x 유지
- `AspNetCore.HealthChecks.Redis` 최신: **9.0.0** (10.x 미출시)

---

## 2. 요구사항

| ID | 요구사항 | 우선순위 |
|----|----------|----------|
| F-01 | `global.json`: SDK `9.0.100` → `10.0.300` | P0 |
| F-02 | 13개 `.csproj`: `net8.0`/`net9.0` → `net10.0` | P0 |
| F-03 | EF Core 패키지: 8.x → 9.0.16 (Pomelo 9.0.0 제약) | P0 |
| F-04 | `Pomelo.EntityFrameworkCore.MySql`: 8.0.2 → 9.0.0 | P0 |
| F-05 | `AspNetCore.HealthChecks.Redis`: 8.0.1 → 9.0.0 | P1 |
| F-06 | `Microsoft.AspNetCore.Mvc.Testing`: → 10.0.8 | P1 |
| F-07 | 6개 `Dockerfile`: base image → `10.0` | P1 |
| F-08 | `.github/workflows/ci.yml`: `dotnet-version: 9.0.x` → `10.0.x` | P1 |
| F-09 | .NET 10 breaking change 대응: `RedisValue` 오버로드 모호성, EF Core 9 다중 provider 검증 | P0 |

---

## 3. 구현 요약

### .NET 10 Breaking Changes 수정
- `RedisValue` → `int.TryParse` / `JsonSerializer.Deserialize` 오버로드 모호성: `(string?)` / `(string)` 명시적 캐스팅 (3개 파일)
- `AuthTestWebAppFactory`: EF Core 9 신규 검증(`IDatabaseProvider` 복수 등록 금지)으로 기존 `AddDbContextFactory(InMemory)` 패턴 실패 → `InMemoryDbContextFactory` 직접 등록으로 변경
- `Microsoft.AspNetCore.OpenApi 10.x`가 `Microsoft.OpenApi 2.x` 당겨와 Swashbuckle 6.x와 충돌 → `9.0.0` 유지

---

## 4. 검증 결과

- `dotnet build PlatformA.sln` — 오류 0개
- `dotnet test PlatformA.sln` — **113개 전부 통과** (net10.0 대상)
