# 요구사항 명세: AutomateCiFailureDetection

작성일: 2026-06-04
브랜치: 2026-06-04_AutomateCiFailureDetection
소스: 대화 컨텍스트 (PR #70 실패 분석 + Phase 3 n8n/PostgreSQL 활용 계획)

## 요구사항 요약

PR #70에서 발생한 3가지 CI 실패(BOM 인코딩, impact null, IDE0160 namespace)를 교훈으로,
동일 패턴 실패를 자동 감지·기록·수정하는 파이프라인을 구축한다.
이미 구축된 n8n + PostgreSQL(`sdlc.ai_failures` 테이블) 인프라를 활용한다.

## 상세 요구사항

### 1. `/done` 스킬 — format 자동 수정 (가장 즉각적 방어선)

**현재 동작**: format 실패 시 중단 + 수동 수정 안내
**개선**: format 실패 감지 → 자동으로 `dotnet format` 실행 → "chore: auto-fix dotnet format" 커밋 → 재검증 → 계속

```
3단계 변경:
  dotnet format --verify-no-changes 실패
    → dotnet format whitespace --no-restore (자동 수정)
    → dotnet format style --no-restore
    → git add -A && git commit -m "chore: auto-fix dotnet format"
    → dotnet format --verify-no-changes 재실행 (통과 확인)
    → 계속 진행
```

### 2. `.github/workflows/auto-format.yml` — CI format 실패 자동 수정

format 관련 CI 실패 시 GitHub Actions에서 자동으로 fix 커밋을 생성·push:
- 트리거: CI Build & Test 실패 후 format 오류 감지
- 단계: `dotnet format` → 변경사항 감지 → commit "chore[ci]: auto-fix dotnet format" → push

### 3. `.github/scripts/mark_ci_failure.py` — CI 실패 분류·기록

ci.yml에서 이미 호출 중이지만 파일이 없음. 구현:
- 실패 타입 분류: `format_failed` / `style_failed` / `build_failed` / `test_failed`
- GitHub PR comment에 구조화된 실패 정보 기록 (n8n이 폴링할 수 있는 형태)
- `fixable_by_ai` 판단: format_failed/style_failed → True, build/test → 분석 필요

### 4. `.github/scripts/record_failure.py` — 로컬 PostgreSQL ai_failures INSERT

로컬에서 실행하는 실패 기록 스크립트:
```python
# 사용법:
record_failure.py --type format_failed --branch BRANCH --message "msg" [--resolved]
record_failure.py --list-unresolved --branch BRANCH  # 미해결 목록 조회
```
- PostgreSQL `sdlc.ai_failures` 테이블에 INSERT/SELECT
- 환경변수 `SDLC_DB_CONNECTION` 사용

### 5. `.n8n/workflows/github-failure-monitor.json` — n8n 워크플로 (핵심)

n8n이 GitHub API를 폴링하여 실패를 감지하고 PostgreSQL에 기록:
```
Schedule (10분마다)
  → GitHub API: GET /repos/rumdice/platformA/actions/runs?status=failure&per_page=5
  → 각 실패 run에 대해:
    → GitHub API: GET /repos/.../actions/runs/{id}/jobs (로그 추출)
    → 실패 타입 분류 (format / build / test)
    → PostgreSQL 노드: INSERT INTO sdlc.ai_failures
    → (미래) Slack 알림
```
워크플로는 JSON 파일로 저장 → n8n에서 import 가능.

### 6. `.claude/hooks/session-start.sh` — ai_failures 미해결 건 표시

세션 시작 시 현재 브랜치의 미해결 실패를 PostgreSQL에서 조회:
```bash
# 빌드 상태 확인 후 추가
python3 .github/scripts/record_failure.py --list-unresolved --branch CURRENT_BRANCH
→ 출력: "[미해결 CI 실패] format_failed — InitialSdlcDb.cs 인코딩 (2026-06-04)"
```

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|------|----------|
| `.claude/skills/done/SKILL.md` | 수정 (3단계 auto-fix 추가) |
| `.github/workflows/auto-format.yml` | 신규 |
| `.github/scripts/mark_ci_failure.py` | 신규 |
| `.github/scripts/record_failure.py` | 신규 |
| `.n8n/workflows/github-failure-monitor.json` | 신규 |
| `.claude/hooks/session-start.sh` | 수정 (ai_failures 조회 추가) |

## 제약 및 주의사항

- **GitHub Actions ↔ 로컬 PostgreSQL 연결 불가**: `mark_ci_failure.py`는 PostgreSQL에 직접 쓰지 않고 GitHub PR comment에 구조화 데이터를 남김 → n8n이 로컬에서 폴링 후 PostgreSQL INSERT
- **n8n GitHub 토큰 필요**: n8n 워크플로에서 GitHub API 호출 시 `GITHUB_PAT` 환경변수 필요 (`.env` 파일에 추가)
- **record_failure.py 로컬 전용**: PostgreSQL이 실행 중인 경우에만 동작 (없으면 graceful skip)
- **n8n 워크플로 import 필요**: JSON 파일 생성 후 n8n UI에서 수동 import 또는 `docker exec` CLI import

## 구현 접근 방향

레이어 1 (로컬 차단): `/done` 스킬 auto-fix → 대부분의 format 실패를 CI 전에 차단
레이어 2 (CI 자동 수정): `auto-format.yml` → 슬립쓰루한 format 실패 자동 처리
레이어 3 (실패 기록): `mark_ci_failure.py` → PR comment에 구조화 실패 정보
레이어 4 (n8n 통합): `github-failure-monitor.json` → 로컬 PostgreSQL에 실패 누적
레이어 5 (세션 연속성): `session-start.sh` → 다음 세션에 미해결 실패 표시

## 검증 기준

- [ ] `/done` 실행 시 format 불일치 → 자동 수정 커밋 생성 후 계속 진행 확인
- [ ] `auto-format.yml`이 format 관련 CI 실패 시 fix 커밋을 PR에 push하는지 확인
- [ ] `mark_ci_failure.py` 실행 시 PR comment 생성 확인
- [ ] `record_failure.py --list-unresolved` 실행 시 PostgreSQL 조회 확인
- [ ] n8n 워크플로 import 후 Schedule 노드 실행 확인
- [ ] `session-start.sh`에서 미해결 실패 출력 확인
