# Utils.API /readyz 헬스체크 추가

## 작업 목적
Utils.API에 `/healthz` (liveness) + `/readyz` (readiness) 엔드포인트를 추가한다.
다른 API 서비스들(Auth, Ticketing, Matching)과 동일한 헬스체크 패턴을 적용하여
운영 환경 로드밸런서 및 k8s probe에서 활용할 수 있도록 한다.

## 상세 요구사항

1. `Program.cs`에 `AddHealthChecks()` 등록
   - Redis 연결 체크: `.AddRedis(Consts.REDIS_CONNECTION_STRING, name: "redis", tags: ["readiness"])`
   - `/healthz`: liveness — `Predicate = _ => false` (항상 200)
   - `/readyz`: readiness — Redis 포함, 503 가능

2. 기존 Auth.API/Ticketing.API/Matching.API의 헬스체크 패턴을 정확히 따른다
   - `WriteJsonResponse` 헬퍼 함수 재사용 (이미 다른 API에 있으면 참조, 없으면 인라인 작성)
   - `[Route("api/[controller]")]` 패턴과 충돌하지 않도록 `/healthz`, `/readyz` prefix 확인

3. 기존 `Program.cs` 구조를 최소한으로 변경한다 — 헬스체크 등록과 미들웨어 추가만

## 제약 및 주의사항
- `Consts.cs`에 이미 `REDIS_CONNECTION_STRING`이 있으므로 하드코딩 금지
- `IDbContextFactory` 패턴 변경 없음 (DB Context 변경 아님)
- `/readyz`에 DB 체크는 추가하지 않는다 (Utils.API는 Redis 위주)

## 검증 기준
- `dotnet build PlatformA.sln` 오류 없음
- `dotnet test PlatformA.sln` 전체 통과
- Utils.API 실행 후 `curl http://localhost:{port}/healthz` → 200
- Utils.API 실행 후 `curl http://localhost:{port}/readyz` → 200 (Redis 연결 시)
