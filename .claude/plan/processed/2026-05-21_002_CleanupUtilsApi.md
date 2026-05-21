# 요구사항 명세: CleanupUtilsApi

작성일: 2026-05-21
소스: plan mode (aws-wiggly-bee.md)

## 요구사항 요약

PlatformA.Utils.API의 코드 품질 기준 통일.
주석으로 남은 대체 코드 제거, Redis 키 상수화, SQLite 연결 문자열 외부화.

## 상세 요구사항

1. 주석 처리된 죽은 코드 3곳 제거
   - `UtilController.cs` L92: Guid 방식 주석
   - `UtilController.cs` L120-122: 메모리 DB 방식 주석
   - `ShortUrl.cs` L5: 구 int Id 주석

2. Redis 키 하드코딩 → Consts.cs 상수 교체
   - `"url:{code}"`, `"stats:{code}"`, `"dirty_codes"` 3개
   - 프로젝트 규칙: 하드코딩 문자열 키 사용 금지 (CLAUDE.md)

3. SQLite 연결 문자열 → appsettings.json 이동
   - `Program.cs`의 `"Data Source=app.db"` → `GetConnectionString("DefaultConnection")`

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|------|---------|
| `PlatformA.Utils.API/Controllers/UtilController.cs` | 주석 제거 + Redis 키 교체 |
| `PlatformA.Utils.API/Models/DB/ShortUrl.cs` | 주석 제거 |
| `PlatformA.Utils.API/Program.cs` | 연결 문자열 외부화 |
| `PlatformA.Utils.API/appsettings.json` | ConnectionStrings 추가 |
| `PlatformA.Library/Common/Consts.cs` | Short URL Redis 키 상수 3개 추가 |

## 제약 및 주의사항

- `Consts.cs` 이외 위치에 Redis 키 하드코딩 금지 (CLAUDE.md 규칙)
- TTL(10분), 동기화 주기(5000ms), GeoIP TODO, CORS 설정은 이번 범위 외

## 구현 접근 방향

1. `Consts.cs`에 키 3개 추가
2. `UtilController.cs` 주석 제거 + `string.Format(Consts.키, code)` 교체
3. `ShortUrl.cs` 주석 제거
4. `Program.cs` + `appsettings.json` 연결 문자열 이동

## 검증 기준

- `dotnet build PlatformA.sln` 오류 0개
- `dotnet test PlatformA.sln` 기존 29개 테스트 통과
- `UtilController.cs`, `ShortUrl.cs`에 `//` 주석 코드 없음
- `Consts.cs`에 `REDIS_SHORT_URL_KEY`, `REDIS_SHORT_URL_STATS_KEY`, `REDIS_DIRTY_CODES_KEY` 존재
