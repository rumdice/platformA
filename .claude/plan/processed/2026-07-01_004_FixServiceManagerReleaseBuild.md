# 요구사항 명세: FixServiceManagerReleaseBuild

작성일: 2026-07-01
브랜치: 2026-07-01_FixServiceManagerReleaseBuild
소스: DB job summary (sprint-083.md)

## 요구사항 요약

ServiceManager.Launch()가 `dotnet run --no-build`(기본 Debug 구성)로 서비스를 기동하는데, `/e2e` 스킬이 `dotnet build -c Release`만 실행하므로 `bin/Debug/net10.0/`에 실행 파일이 없어 서비스가 즉시 종료된다. `dotnet run -c Release --no-build`로 수정하여 E2E 시나리오 10의 서비스 기동 실패를 해결한다.

## 상세 요구사항

1. `ServiceManager.cs`의 `Launch()` 메서드에서 `dotnet run --no-build` → `dotnet run -c Release --no-build`로 변경
2. launch profile 유무 모두에 `-c Release` 적용:
   - launch profile 없음: `run -c Release --no-build --project "{projPath}"`
   - launch profile 있음: `run -c Release --no-build --project "{projPath}" --launch-profile {profile}`

## 영향 범위 (예상)

| 파일 | 변경 내용 |
|------|---------|
| `PlatformA/PlatformA.Game.DummyClient/ServiceManager.cs` | Launch() 메서드 args 문자열 2줄 수정 |

테스트 코드 변경 없음 (ServiceManager는 E2E 런타임 전용, 유닛 테스트 대상 아님)

## 제약 및 주의사항

- DummyClient 자체도 `-c Release --no-build`로 실행되므로 일관성 유지
- launchSettings.json의 profile 이름(`https`)은 변경하지 않음
- 기존 시나리오 9는 스킬이 직접 서비스를 기동하므로 영향 없음

## 구현 접근 방향

```csharp
// 변경 전
string args = string.IsNullOrEmpty(spec.LaunchProfile)
    ? $"run --no-build --project \"{projPath}\""
    : $"run --no-build --project \"{projPath}\" --launch-profile {spec.LaunchProfile}";

// 변경 후
string args = string.IsNullOrEmpty(spec.LaunchProfile)
    ? $"run -c Release --no-build --project \"{projPath}\""
    : $"run -c Release --no-build --project \"{projPath}\" --launch-profile {spec.LaunchProfile}";
```

## 검증 기준

- `dotnet run -c Release --no-build`로 Auth.API를 수동 실행하면 `https://localhost:7001/healthz`가 200 응답
- E2E 시나리오 10 실행 시 ServiceManager가 5/5 서비스 기동 성공 (0/5 → 5/5)
- 빌드·테스트 전체 통과 (ServiceManager.cs 변경은 빌드만 영향)
