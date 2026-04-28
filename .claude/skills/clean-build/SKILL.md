---
name: clean-build
description: Visual Studio/dotnet CLI 캐시 충돌(MSB3492)을 해결한다. dotnet clean 후 전체 솔루션을 재빌드하여 깨끗한 빌드 상태를 확보한다.
disable-model-invocation: true
allowed-tools: Bash(dotnet *)
---

# PlatformA Clean Build

현재 브랜치: !`git branch --show-current`
솔루션 경로: PlatformA/PlatformA.sln

---

## 수행 순서

### Step 1: Generator.Lib 캐시 클린

MSB3492 오류는 PlatformA.Generator.Lib의 캐시 파일 충돌이 원인이다.
Generator 프로젝트를 먼저 클린한다:

```bash
cd PlatformA && dotnet clean PlatformA.Generator.Lib/PlatformA.Generator.Lib.csproj -q
```

### Step 2: 전체 솔루션 클린

```bash
dotnet clean PlatformA.sln -q
```

### Step 3: 전체 솔루션 재빌드

```bash
dotnet build PlatformA.sln
```

빌드 실패 시: 오류 메시지를 전체 출력하고 즉시 중단한다.

### Step 4: 결과 보고

아래 형식으로 결과를 출력한다:

| 항목 | 결과 |
|------|------|
| Clean | ✅ 완료 / ❌ 실패 |
| Build 오류 수 | N개 |
| Build 경고 수 | N개 |
| push 가능 여부 | ✅ 가능 / ❌ 불가 |

빌드 성공 시:
> "clean-build 완료. `/build-check`로 테스트까지 검증하세요."

빌드 실패 시:
> "빌드 오류가 남아 있습니다. 오류를 수정한 뒤 다시 실행하세요."
