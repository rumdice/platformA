---
name: plan
description: 새 작업 계획을 수립한다. PR 머지 여부를 먼저 확인하고, 머지됐으면 main 최신화 후 새 브랜치를 생성한다. PR 미머지 상태에서는 경고 후 중단한다.
disable-model-invocation: true
allowed-tools: Bash(git *) Bash(gh *) Read Edit
---

# 새 작업 계획 수립

## 컨텍스트
- 오늘 날짜: !`date +%Y-%m-%d`
- 현재 브랜치: !`git branch --show-current`
- 현재 브랜치 PR 상태: !`export PATH="/c/Program Files/GitHub CLI:$PATH"; branch=$(git branch --show-current); gh pr list --head "$branch" --state all --json number,state,title --limit 1 2>/dev/null || echo "[]"`
- 오늘 원격 브랜치 수: !`git ls-remote --heads origin 2>/dev/null | grep -c "refs/heads/$(date +%Y-%m-%d)" || echo "0"`

## 사용자 작업 설명
$ARGUMENTS

---

## 수행 순서

### 사전 검사

`$ARGUMENTS`가 비어 있으면 작업 설명을 요청하고 **중단**한다.

#### 현재 브랜치가 `main`이 아닌 경우

"현재 브랜치 PR 상태"를 분석한다.

**케이스 A — `state: "MERGED"`**

PR이 main에 머지된 것이 확인됐다. 자동으로 main을 최신화한다:

```
✅  PR #{번호} 가 main에 머지된 것을 확인했습니다.
    main 최신화 후 새 브랜치를 생성합니다.
```

```bash
git checkout main
git pull origin main
```

이후 1단계로 진행한다.

---

**케이스 B — `state: "OPEN"`**

즉시 **중단**한다:

```
⚠️  중단: 현재 브랜치 '{브랜치명}'에 아직 머지되지 않은 PR #{번호}이 열려 있습니다.

    다음 중 하나를 선택하세요:
      1. GitHub에서 PR을 main에 머지한 뒤 /plan 재실행
      2. git checkout main 후 /plan 실행 (기존 브랜치 작업은 유지됨)
```

---

**케이스 C — PR 없음 (빈 배열 `[]` 또는 결과 없음)**

즉시 **중단**한다:

```
⚠️  중단: 현재 브랜치 '{브랜치명}'에 연결된 PR이 없습니다.
    미커밋 또는 미완료 작업이 남아 있을 수 있습니다.

    다음 중 하나를 선택하세요:
      1. /done 으로 현재 작업을 완료 처리한 뒤 /plan 재실행
      2. git checkout main 후 /plan 실행
```

---

#### 현재 브랜치가 `main`인 경우

사전 검사 생략, 1단계로 바로 진행한다.

---

### 1단계: PlanName 생성

사용자 설명을 분석하여 PascalCase 영문 PlanName을 생성한다.
- 규칙: 동사+명사 형태, 최대 30자 (예: AddAuthTests, FixRedisBug, ImproveWorkflow)

---

### 2단계: N 계산 — 당일 통합 카운터

"오늘 원격 브랜치 수"를 사용한다. **PlanName에 무관하게 당일 전체 브랜치 수 기준**으로 계산한다.

```
N = 오늘 원격 브랜치 수 + 1
```

최종 브랜치명: `오늘날짜_PlanName_N`

예시 (당일 브랜치가 이미 1개 있을 때):
```
2026-04-30_AddVSCodeBuildEnv_1   ← 오늘의 1번째 작업 브랜치 (기존)
2026-04-30_ImproveWorkflow_2     ← 오늘의 2번째 작업 브랜치 (신규, PlanName 달라도 N=2)
```

---

### 3단계: 브랜치 생성 및 push

(사전 검사에서 main 위에 있음이 보장된 상태)

```bash
git checkout -b {브랜치명}
git push -u origin {브랜치명}
```

---

### 4단계: SPRINT.md 업데이트

`AI/SPRINT.md`를 읽어 가장 최근 스프린트의 `### 진행 중` 섹션에 태스크를 추가한다.
- 사용자 설명을 기반으로 구체적인 태스크 항목 2~5개를 `- [ ]` 형식으로 작성한다.
- 항목은 검증 가능한 단위로 쪼갠다.

```bash
git add AI/SPRINT.md
git commit -m "계획: {PlanName} 태스크 추가"
git push
```

---

### 5단계: 완료 보고

```
브랜치: {브랜치명}  (오늘 {N}번째 작업)

SPRINT.md 추가 항목:
  - [ ] ...
  - [ ] ...

작업을 완료하면 /done 을 실행하세요.
```
