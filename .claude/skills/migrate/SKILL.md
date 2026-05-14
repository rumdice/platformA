---
name: migrate
description: EF Core 마이그레이션을 생성하고 선택적으로 DB에 적용한다. DB 변경이므로 각 단계마다 사용자 확인 후 진행한다. 사용법: /migrate WebApp AddRatingColumn
allowed-tools: Bash(dotnet ef *) Bash(dotnet build *) Read
---

# EF Core 마이그레이션 관리

## 컨텍스트
- 현재 브랜치: !`git branch --show-current`
- WebApp Migration 목록: !`cd PlatformA/PlatformA.MySqlDB.Lib && dotnet ef migrations list --context DbWebAppContext 2>/dev/null | tail -5 || echo "(조회 실패 — EF CLI 또는 DB 연결 확인 필요)"`
- LogApp Migration 목록: !`cd PlatformA/PlatformA.MySqlDB.Lib && dotnet ef migrations list --context DbLogAppContext 2>/dev/null | tail -5 || echo "(조회 실패 — EF CLI 또는 DB 연결 확인 필요)"`

## 사용자 입력
$ARGUMENTS

---

## 수행 순서

### 사전 검사

`$ARGUMENTS`가 비어 있으면 다음 사용법을 출력하고 **중단**한다:

```
사용법: /migrate <컨텍스트> <마이그레이션이름>

  컨텍스트: WebApp 또는 LogApp
  이름:     PascalCase 영문 (동사+명사 형식 권장)

예시:
  /migrate WebApp AddRatingColumn
  /migrate WebApp AddMatchRecordsTable
  /migrate LogApp AddMatchLog
```

`$ARGUMENTS`를 파싱한다:
- 첫 번째 토큰 → CONTEXT
- 두 번째 토큰 → MIGRATION_NAME

CONTEXT가 `WebApp` 또는 `LogApp` 외의 값이면 오류를 출력하고 중단한다.
MIGRATION_NAME이 비어 있으면 이름 입력을 요청하고 중단한다.
MIGRATION_NAME이 PascalCase가 아니면 경고를 출력하되 계속 진행한다.

---

### Step 1: 빌드 사전 검증

Migration 생성 전 빌드가 성공해야 한다.

```bash
cd PlatformA && dotnet build PlatformA.sln -q
```

빌드 실패 시 **즉시 중단**:
> "빌드 오류가 있습니다. `/build-check`로 먼저 빌드를 수정하세요."

---

### Step 2: Migration 생성

CONTEXT에 따라 명령을 선택하여 실행한다:

**WebApp 컨텍스트:**
```bash
cd PlatformA/PlatformA.MySqlDB.Lib && dotnet ef migrations add {MIGRATION_NAME} \
  --context DbWebAppContext \
  --output-dir Migrations/WebApp
```

**LogApp 컨텍스트:**
```bash
cd PlatformA/PlatformA.MySqlDB.Lib && dotnet ef migrations add {MIGRATION_NAME} \
  --context DbLogAppContext \
  --output-dir Migrations/LogApp
```

생성 실패(이미 같은 이름 존재, 변경사항 없음 등) 시 오류를 설명하고 중단한다.

---

### Step 3: Migration 내용 검토 + 사용자 확인

생성된 Migration 파일(`PlatformA/PlatformA.MySqlDB.Lib/Migrations/{CONTEXT}/{타임스탬프}_{MIGRATION_NAME}.cs`)을 읽어
Up() / Down() 메서드의 핵심 변경사항을 요약한다:

```
생성된 Migration: {파일명}

변경 내용:
  Up() (적용):
    - <테이블/컬럼 추가·수정·삭제 요약>
  Down() (롤백):
    - <롤백 내용 요약>

⚠️  로컬 DB에 적용하시겠습니까?
    yes → dotnet ef database update 실행
    no  → 여기서 중단 (Migration 파일은 생성된 상태 유지, /done으로 커밋 가능)
```

사용자가 명확히 "yes"라고 하지 않는 한 Step 4로 진행하지 않는다.

---

### Step 4: Migration 적용 (사용자 승인 후)

**WebApp:**
```bash
cd PlatformA/PlatformA.MySqlDB.Lib && dotnet ef database update --context DbWebAppContext
```

**LogApp:**
```bash
cd PlatformA/PlatformA.MySqlDB.Lib && dotnet ef database update --context DbLogAppContext
```

---

### Step 5: 완료 보고

```
Migration 완료
  이름:     {MIGRATION_NAME}
  컨텍스트: {CONTEXT}
  DB 적용:  ✅ 완료 / ⏭ 건너뜀

다음 단계:
  - /done 실행 시 Migration 파일이 자동 커밋됩니다.

롤백이 필요한 경우:
  cd PlatformA/PlatformA.MySqlDB.Lib
  dotnet ef database update <이전Migration이름> --context Db{CONTEXT}Context
```
