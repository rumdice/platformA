---
name: qa-failure
schema_version: 1
description: GitHub Actions CI 실패를 자동 분석하고 failure_type·fixable·recommended_fix를 보고한다. gh CLI로 실패 로그를 가져와 BUILD/FORMAT/TEST 유형으로 분류하고 수정 방향을 제시한다.
allowed-tools: Bash(gh *) Grep Read
---

# /qa-failure — CI 실패 자동 분석

## 인수
```
/qa-failure [run-id]
```
- `run-id` 생략 시: 가장 최근 실패 런 자동 선택
- `run-id` 지정 시: 해당 런의 로그 직접 분석

---

## 수행 순서

### 1단계 — 최근 실패 런 조회

```bash
export PATH="$PATH:/c/Program Files/GitHub CLI/"
gh run list --status failure -L 5 --json databaseId,displayTitle,conclusion,createdAt,headBranch,url
```

인수가 없으면 가장 최근 실패 런의 `databaseId`를 선택한다.
인수가 있으면 그 값을 `RUN_ID`로 사용한다.

### 2단계 — 실패 로그 추출

```bash
export PATH="$PATH:/c/Program Files/GitHub CLI/"
gh run view $RUN_ID --log-failed 2>&1 | head -200
```

로그가 너무 길면 `head -200`으로 앞부분을 우선 분석하고
오류 키워드 위치를 파악한 뒤 필요 시 추가로 읽는다.

### 3단계 — 실패 유형 분류

로그에서 아래 키워드로 실패 유형을 판정한다:

| 유형 | 판정 키워드 |
|------|-----------|
| **BUILD** | `error CS`, `error MSB`, `Build FAILED`, `Could not resolve` |
| **FORMAT** | `dotnet format`, `whitespace`, `style`, `Formatted code`, `Run dotnet format` |
| **TEST** | `Failed!`, `FAILED`, `xUnit`, `Assert.`, `Test Run Failed` |

복수 유형이 감지되면 우선순위: BUILD > TEST > FORMAT

### 4단계 — 분석 보고

아래 형식으로 보고한다:

```
## CI 실패 분석 결과

- **Run ID**: {id}
- **브랜치**: {branch}
- **실패 시각**: {createdAt}
- **URL**: {url}

### 진단
- **failure_type**: BUILD | FORMAT | TEST
- **fixable_by_ai**: true | false
- **error_summary**:
  (핵심 오류 메시지 5줄 이내)

### 권고 수정 방향
(파일명과 라인 번호를 포함한 구체적 수정 방법)
```

### 5단계 — 수정 제안

`fixable_by_ai: true`인 경우:
- 수정 방향을 구체적으로 제안하고 사용자에게 직접 수정 여부를 묻는다
- 사용자가 동의하면 즉시 파일을 수정하고 `dotnet build PlatformA.sln`으로 검증

`fixable_by_ai: false`인 경우:
- 원인 분석과 수동 수정에 필요한 정보를 제공한다

---

## 실패 유형별 대응 가이드

### BUILD 실패
- CS 오류: 파일경로:라인의 컴파일 오류 → 직접 수정 가능
- MSB3492 (cache 오류): `dotnet clean PlatformA.sln && dotnet build PlatformA.sln` 권고
- 패키지 해석 실패: `dotnet restore` 권고

### FORMAT 실패
- `dotnet format PlatformA/PlatformA.sln --verify-no-changes` 실패
- 대응: `dotnet format PlatformA/PlatformA.sln` 실행 후 커밋

### TEST 실패
- 실패 테스트 클래스와 메서드명 특정 후 원인 분석
- Mock 설정 불일치 / 비즈니스 로직 버그 / 환경 차이 구분
