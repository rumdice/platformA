---
name: build-check
description: push 전 필수 검증. PlatformA 솔루션 전체 빌드 및 테스트를 순서대로 실행하고 결과를 보고한다.
disable-model-invocation: true
allowed-tools: Bash(dotnet *)
---

# PlatformA Push 전 빌드 검증

현재 브랜치: !`git branch --show-current`
변경 파일 수: !`git diff --name-only HEAD~1 2>/dev/null | wc -l`

CLAUDE.md의 "Push 전 필수 빌드 검증 절차"를 아래 순서로 실행한다.

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
| push 가능 여부 | ✅ 가능 / ❌ 불가 |

빌드 또는 테스트 실패 시 → **push 금지**, 오류 원인과 수정 방법을 안내한다.
둘 다 통과 시 → push 가능 확인 메시지 출력.
