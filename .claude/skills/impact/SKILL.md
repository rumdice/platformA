---
name: impact
schema_version: 1
description: 현재 브랜치의 변경 파일을 분석하여 영향 범위와 위험도를 평가한다. /plan 직후 또는 코드 수정 전 실행하여 IMPACT_ANALYSIS 단계를 수행한다.
allowed-tools: Bash(git *) Bash(ls *) Bash(grep *) Bash(rg *) Read Glob
---

# /impact — 변경 영향 범위 분석

## 컨텍스트
- 현재 브랜치: !`git branch --show-current`
- main 대비 변경 파일: !`git diff --name-only origin/main...HEAD 2>/dev/null || echo "(변경 없음)"`
- 미커밋 변경 파일: !`git diff --name-only; git diff --name-only --cached`
- 오늘 명세 파일: !`ls .claude/plan/$(date +%Y-%m-%d)_*.md 2>/dev/null || echo "(없음)"`

---

## 수행 순서

### 1단계 — 분석 대상 파일 수집

**git diff가 있는 경우 (코드 수정 후)**: main 대비 변경 파일 + 미커밋 변경 파일을 합산한다.

```bash
git diff --name-only origin/main...HEAD 2>/dev/null
git diff --name-only
git diff --name-only --cached
```

**git diff가 없는 경우 (코드 수정 전 — /plan 직후)**: 오늘 명세 파일의 "영향 범위" 섹션을 읽어 예정 변경 파일 목록을 수집한다.

```bash
# 가장 최근 명세 파일 읽기
ls .claude/plan/$(date +%Y-%m-%d)_*.md 2>/dev/null | sort | tail -1
```

명세 파일의 `## 영향 범위` 테이블에서 파일 경로를 추출하여 분석 대상으로 사용한다.
분석 결과 앞에 `(사전 분석 — 코드 수정 전)` 레이블을 붙인다.

---

### 2단계 — 파일 분류 및 위험도 평가

각 파일을 아래 기준으로 분류한다:

| 분류 | 해당 경로 패턴 | 기본 위험도 |
|------|--------------|-----------|
| **핵심 라이브러리** | `PlatformA.Library/` | 🔴 HIGH — 전 서비스 영향 |
| **DB 스키마** | `Migrations/`, `*Context.cs`, `Entities/` | 🔴 HIGH — 데이터 손실 위험 |
| **인증/보안** | `*Auth*`, `*Token*`, `*Jwt*` | 🔴 HIGH — 보안 영향 |
| **API 컨트롤러** | `Controllers/` | 🟡 MEDIUM — 엔드포인트 변경 |
| **서비스 레이어** | `Services/` | 🟡 MEDIUM — 비즈니스 로직 변경 |
| **Redis/캐시** | `Redis*`, `*Cache*`, `Consts.cs` | 🟡 MEDIUM — 분산 상태 영향 |
| **테스트 코드** | `*.Tests.*`, `*Tests.cs` | 🟢 LOW — 테스트만 영향 |
| **설정/문서** | `*.json`, `*.md`, `*.yml` | 🟢 LOW |
| **스킬/AI** | `.claude/` | 🟢 LOW |

---

### 3단계 — 참조 관계 확인

HIGH/MEDIUM 파일에 대해 다른 파일에서 참조하는지 확인한다:

```bash
# 변경된 클래스명/메서드명이 다른 파일에서 참조되는지 검색
# 예: 변경 파일이 RedisManager.cs 이면
rg "RedisManager" --type cs -l 2>/dev/null | head -20
```

---

### 4단계 — 테스트 커버리지 확인

변경 파일에 대응하는 테스트 파일이 있는지 확인한다:

```bash
# 예: PlatformA.Auth.API/Controllers/AuthController.cs → 
#     PlatformA.Tests.Auth.API/AuthControllerTests.cs 존재 여부
ls PlatformA/PlatformA.Tests.*/  2>/dev/null | head -30
```

---

### 5단계 — 영향 분석 보고

아래 형식으로 보고한다:

```
## 영향 범위 분석

### 변경 파일 요약
- 총 변경 파일: N개
- HIGH 위험: N개  
- MEDIUM 위험: N개
- LOW 위험: N개

### 위험 파일 목록
| 파일 | 분류 | 위험도 | 이유 |
|------|------|--------|------|
| ... | ... | 🔴 HIGH | ... |

### 참조 관계
- {파일명}: {N}개 파일에서 참조됨 → 변경 시 파급 효과 주의

### 테스트 커버리지
- ✅ 커버됨: {파일 목록}
- ❌ 미커버: {파일 목록} ← 테스트 추가 권장

### 종합 위험도: 🔴 HIGH | 🟡 MEDIUM | 🟢 LOW

### 권고사항
{위험도에 따른 주의사항 또는 추가 조치}
```

---

## 위험도별 권고 행동

| 종합 위험도 | 권고 행동 |
|-----------|---------|
| 🔴 HIGH | `/review` 스킬로 코드 리뷰 후 PR 생성 권장. DB 변경 시 `db-migrator` 에이전트 사용 |
| 🟡 MEDIUM | 관련 테스트 통과 확인 필수. 영향 받는 API는 `/doc-writer api-guide` 로 문서 동기화 |
| 🟢 LOW | 정상 `/done` 흐름으로 진행 가능 |
