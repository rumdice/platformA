---
name: plan
description: 새 작업 계획을 수립한다. 사용자 설명에서 PlanName을 자동 생성하고, 오늘 날짜 기반 브랜치를 만들어 원격에 push한 뒤 SPRINT.md를 업데이트한다.
disable-model-invocation: true
allowed-tools: Bash(git *) Read Edit
---

# 새 작업 계획 수립

## 컨텍스트
- 오늘 날짜: !`date +%Y-%m-%d`
- 현재 브랜치: !`git branch --show-current`
- 오늘 생성된 원격 브랜치: !`git ls-remote --heads origin 2>/dev/null | grep "$(date +%Y-%m-%d)" | sed 's/.*refs\/heads\///' | sort`

## 사용자 작업 설명
$ARGUMENTS

---

## 수행 순서

### 사전 검사
- 현재 브랜치가 main이 아니면 "이미 작업 브랜치에 있습니다" 경고 후 계속할지 확인한다.
- `$ARGUMENTS`가 비어 있으면 작업 설명을 요청하고 중단한다.

### 1단계: PlanName 생성
사용자 설명을 분석하여 PascalCase 영문 PlanName을 생성한다.
- 규칙: 동사+명사 형태, 최대 30자 (예: AddAuthTests, FixRedisBug, RefactorPacketHandler)
- 위 "오늘 생성된 원격 브랜치" 목록에서 동일한 PlanName이 이미 있는지 확인한다.

### 2단계: N 계산
"오늘 생성된 원격 브랜치" 목록에서 `오늘날짜_PlanName_` 패턴과 일치하는 브랜치 수를 센다.
- N = 일치 수 + 1
- 최종 브랜치명: `오늘날짜_PlanName_N` (예: `2026-04-25_AddAuthTests_1`)

### 3단계: main 최신화 후 브랜치 생성
```bash
git checkout main
git pull origin main
git checkout -b {브랜치명}
git push -u origin {브랜치명}
```

### 4단계: SPRINT.md 업데이트
`AI/SPRINT.md`를 읽어 가장 최근 스프린트의 `### 진행 중` 섹션에 태스크를 추가한다.
- 사용자 설명을 기반으로 구체적인 태스크 항목 2~5개를 `- [ ]` 형식으로 작성한다.
- 항목은 검증 가능한 단위로 쪼갠다.
- 변경 후 커밋:
```bash
git add AI/SPRINT.md
git commit -m "계획: {PlanName} 태스크 추가"
git push
```

### 5단계: 완료 보고
다음을 출력한다:
```
브랜치: {브랜치명}
SPRINT.md 추가 항목:
  - [ ] ...
  - [ ] ...

작업을 완료하면 /done 을 실행하세요.
```
