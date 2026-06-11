# 요구사항 명세: CleanupPhaseCReferences

작성일: 2026-06-12
브랜치: 2026-06-12_CleanupPhaseCReferences
소스: AI/sprints/sprint-062.md + 세션 분석

## 요구사항 요약

Phase C 전환(PR #92, 2026-06-11)으로 AI/SPRINT.md, AI/cost-log.md, Docs/operations/ai-sdlc.md가 삭제됐으나,
이를 직접 참조하는 코드가 4개 파일에 잔존한다. 이 참조를 제거하여 /sprint, /workreport 정상 동작을
보장하고, pr-merge-sync CI 경고를 없애고, orphan 파일을 삭제한다.

## 상세 요구사항

### 1. `.claude/commands/sprint.md` 수정
- `AI/SPRINT.md`를 읽어 출력하는 로직 제거
- Phase C 기준: DB(db_write.py --action list-active) 조회 우선, 실패 시 AI/sprints/*.md frontmatter fallback
- 출력 형식: sprint number, title, branch, status 표시

### 2. `.claude/skills/workreport/SKILL.md` 수정
- 17줄 sidecar: `` !`tail -40 AI/SPRINT.md` `` → 제거
- 45줄 데이터 수집: `grep "^| ${TODAY}" AI/cost-log.md` → 제거
- 비용/토큰 정보는 DB(sdlc.ai_model_runs) 기반 조회로 대체 or 생략 처리
- "오늘 완료된 task JSON" 조회는 그대로 유지 (아직 AI/tasks/ 는 존재)

### 3. `.github/workflows/pr-merge-sync.yml` 수정
- 34줄 스텝명: `"Regenerate SPRINT.md tables (DB-based, file fallback)"` → `"Sync sprint files (DB-based)"`
- 49줄 git add: `AI/tasks/ AI/SPRINT.md AI/sprints/ AI/cost-log.md .claude/plan/` → `AI/tasks/ AI/sprints/ .claude/plan/`
- 50줄 커밋 메시지: `"자동: PR #${PR_NUMBER} 머지 — task 상태 갱신, SPRINT.md 재생성"` → `"자동: PR #${PR_NUMBER} 머지 — task 상태 갱신, sprints 동기화"`
- generate_sprint_md.py 스텝은 유지 (Phase C에서 파일 없으면 early return하므로 무해함)

### 4. `Docs/operations/ai-sdlc-*.md` 6개 파일 삭제
대상 파일 (내용이 Docs/ai-sdlc/로 이동 완료):
- `Docs/operations/ai-sdlc-append-only-conflict-policy.md`
- `Docs/operations/ai-sdlc-auto-fix-policy.md`
- `Docs/operations/ai-sdlc-db-migration-roadmap.md`
- `Docs/operations/ai-sdlc-job-lock-policy.md`
- `Docs/operations/ai-sdlc-n8n-failure-monitor.md`
- `Docs/operations/ai-sdlc-phase-c-db-only-plan.md`
- toc.yml에 이미 미등재 상태, check_docs_toc.py가 [잔존파일] 경고 발생시킴

### 5. `.claude/hooks/session-start.sh` 정리 (선택)
- 11줄: `SPRINT_FILE="$PROJECT_DIR/AI/SPRINT.md"` 죽은 변수 제거
- 영향 없는 dead code이나 혼란 방지

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|------|---------|
| `.claude/commands/sprint.md` | 수정 |
| `.claude/skills/workreport/SKILL.md` | 수정 |
| `.github/workflows/pr-merge-sync.yml` | 수정 |
| `Docs/operations/ai-sdlc-*.md` (6개) | 삭제 |
| `.claude/hooks/session-start.sh` | 수정 (minor) |

C# 코드 변경 없음. Python 스크립트 변경 없음.

## 제약 및 주의사항

- `generate_sprint_md.py`는 수정하지 않음 — SPRINT.md 없으면 early return, 무해함
- `AI/tasks/` 폴더는 archive 용도로 유지 — workreport에서 계속 참조 가능
- Docs/operations/ 삭제 후 check_docs_toc.py 검사 2번("[잔존파일]") 경고 0건 확인 필수
- `pr-merge-sync.yml` 수정 시 `generate_sprint_md.py || true` 스텝은 그대로 유지

## 구현 접근 방향

1. 파일 5개를 순서대로 수정/삭제 (의존성 없음, 독립적)
2. Docs/operations 삭제는 `git rm` 사용
3. session-start.sh는 마지막에 간단히 정리
4. 각 수정 후 전체 영향 없음 확인 (C# 빌드 영향 없음)

## 검증 기준

1. `rg "AI/SPRINT\.md|AI/cost-log\.md" .claude .github --glob "!*.md 이력/아카이브"` → 0건 (active 스킬/워크플로에서)
2. `python .github/scripts/check_docs_toc.py` → [잔존파일] 경고 0건
3. `dotnet build PlatformA.sln` → 오류 없음
4. `dotnet test PlatformA.sln` → 전체 통과
5. `/sprint` 실행 시 SPRINT.md 없어도 DB/sprints 기반 출력
6. `pr-merge-sync.yml` git add 목록에 AI/SPRINT.md, AI/cost-log.md 없음
