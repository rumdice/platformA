# 요구사항 명세: FixSdlcCiIsolation

작성일: 2026-06-12
브랜치: 2026-06-12_FixSdlcCiIsolation
소스: plan mode (hazy-booping-moore.md)

## 요구사항 요약

GitHub Actions → 로컬 DB 격리 위반 코드를 제거하고, n8n CI 실패 감지를 15분 타임 필터에서 커서 기반 폴링으로 교체하여 노트북 재시작 후에도 과거 실패를 소급 처리한다. /workflow 및 session-start.sh에 미해결 CI 실패 알림을 추가한다.

## 상세 요구사항

1. `.github/workflows/auto-fix.yml` 삭제
   - ANTHROPIC_API_KEY 별도 과금 필요, 실제 실행된 적 없음
   - n8n에서 repository_dispatch 수신 대상 없어짐

2. `.github/workflows/pr-merge-sync.yml` — "Sync sprint files (DB-based)" step 제거 (라인 34-37)
   - GitHub Actions에서 psycopg2로 로컬 DB 접근 시도 → 격리 원칙 위반
   - `generate_sprint_md.py`가 갱신하는 `AI/SPRINT.md`는 Phase C에서 이미 삭제됨

3. `.github/scripts/generate_sprint_md.py` 삭제
   - 대상 파일(AI/SPRINT.md) 없음, psycopg2 DB 접근 코드 포함
   - pr-merge-sync.yml에서 호출 경로 제거 후 고아 스크립트가 됨

4. `.n8n/workflows/github-failure-monitor.json` 수정
   - 제거 노드 5개: `filter-recent`(15분 필터), `fixable-filter`, `postgres-job-lock-claim`, `check-lock-claimed`, `github-dispatch`
   - 추가 노드 1개: `read-cursor` — n8n Static Data에서 last_checked_at 읽기·갱신, 기본값 24시간 전
   - `github-get-failed-runs` 수정: `created >= last_checked_at` 파라미터 추가, per_page 5→20
   - 최종 파이프라인: Schedule → 커서 읽기·갱신 → GitHub CI 조회 → 분리 → Job 상세 → 분류 → PostgreSQL INSERT

5. `.claude/skills/workflow/SKILL.md` — 0.5단계 추가
   - 1단계(/plan) 이전에 `record_failure.py --list-unresolved` 실행
   - 미해결 CI 실패 있으면 사용자에게 표시 (파이프라인 차단 없음)

6. `.claude/hooks/session-start.sh` — main 브랜치 전체 알림 추가
   - 기존 section 4는 non-main 브랜치 전용 → main 브랜치일 때 전체 미해결 실패 표시 추가

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|------|----------|
| `.github/workflows/auto-fix.yml` | 삭제 |
| `.github/workflows/pr-merge-sync.yml` | step 1개 제거 |
| `.github/scripts/generate_sprint_md.py` | 삭제 |
| `.n8n/workflows/github-failure-monitor.json` | 노드 5개 제거, 1개 추가, 1개 수정 |
| `.claude/skills/workflow/SKILL.md` | 0.5단계 삽입 |
| `.claude/hooks/session-start.sh` | 5줄 추가 |

C# 런타임 코드 변경 없음.

## 제약 및 주의사항

- ADR-008(n8n): n8n 워크플로우 변경 범위 내, 신규 ADR 불필요
- ADR-009(PostgreSQL SDLC DB): GitHub Actions DB 접근 금지 원칙 강화 방향으로 일치
- CLAUDE.md "절대 하지 말 것": GitHub Actions 워크플로우에 DB 접근 코드 추가 금지 — 이번 작업은 반대로 제거
- n8n Static Data: n8n 재시작 시 유지됨 (영구 저장), 워크플로우 재import 시 초기화 → 초기값(24시간 전)으로 자동 복구
- n8n 워크플로우 변경은 UI에서 Import 후 활성화 필요 (코드 변경만으로 자동 반영 안 됨)

## 구현 접근 방향

1. git rm으로 삭제 파일 처리 (auto-fix.yml, generate_sprint_md.py)
2. Edit 도구로 pr-merge-sync.yml step 제거
3. JSON 편집으로 n8n 워크플로우 nodes/connections 수정
4. Edit 도구로 workflow/SKILL.md 0단계 뒤 0.5단계 삽입
5. Edit 도구로 session-start.sh section 4 뒤 4.5 블록 삽입

## 검증 기준

- `ls .github/workflows/auto-fix.yml` → 파일 없음
- `ls .github/scripts/generate_sprint_md.py` → 파일 없음
- `pr-merge-sync.yml`에 "SDLC_DB_CONNECTION" 문자열 없음
- n8n JSON 노드 수 7개, `filter-recent` id 없음, `read-cursor` id 있음
- `/workflow` 0.5단계 출력 확인 (record_failure.py --list-unresolved)
- `dotnet build PlatformA.sln` 성공 (C# 변경 없음)
