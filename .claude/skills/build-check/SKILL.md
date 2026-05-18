---
name: build-check
schema_version: 1
description: 현재 코드베이스 상태 빠른 점검. 빌드 오류 및 테스트 실패 여부를 보고한다. 언제든 실행 가능한 읽기 전용 상태 확인용.
disable-model-invocation: true
allowed-tools: Bash(dotnet *) Bash(git *)
---

# PlatformA 빌드 상태 점검

현재 브랜치: !`git branch --show-current`
변경 파일 수: !`git diff --name-only HEAD~1 2>/dev/null | wc -l`

## Step 1: 전체 솔루션 빌드

```bash
cd PlatformA && dotnet build PlatformA.sln
```

빌드 오류가 있으면 **즉시 중단**하고 오류 내용을 보고한다.

## Step 2: 전체 테스트 실행

빌드 통과 후에만 실행한다.

```bash
cd PlatformA && dotnet test PlatformA.sln
```

## Step 3: 결과 보고

| 항목 | 결과 |
|------|------|
| 빌드 오류 수 | ? |
| 테스트 통과 수 | ? |
| 테스트 실패 수 | ? |

오류가 있으면 원인과 수정 방법을 안내한다.
