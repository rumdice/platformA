# 요구사항 명세: ImproveDocFxAiSdlcDocs

작성일: 2026-06-11
브랜치: 2026-06-11_ImproveDocFxAiSdlcDocs
소스: plan mode (hazy-booping-moore.md)

## 요구사항 요약

AI_SDLC 관련 문서 6개가 `Docs/operations/`에 분산·미등재되어 있다.
`Docs/ai-sdlc/` 디렉토리를 신설하여 DocFX 최상위 섹션으로 승격하고,
`generate_ai_sdlc_docs.py`로 반복 문서를 자동 생성하는 기반을 마련한다.

## 상세 요구사항

1. `Docs/toc.yml`에 `AI SDLC` 최상위 섹션 추가 (`운영` 섹션 다음)
2. `Docs/ai-sdlc/` 디렉토리 신설
   - `toc.yml` (15개 항목)
   - `overview.md`, `workflow.md`, `phases.md`, `db-schema.md`, `cost-and-reports.md`, `github-actions.md`, `backup-restore.md`, `troubleshooting.md` (신규 작성)
   - `phase-c-db-only.md`, `job-lock.md`, `auto-fix.md`, `n8n.md` (operations에서 이동)
   - `skill-reference.md`, `automation-map.md`, `gates.md` (자동생성 stub)
3. `Docs/operations/ai-sdlc-*.md` 6개 → `Docs/ai-sdlc/`로 이동
   - `phases.md`: `ai-sdlc-db-migration-roadmap.md` + `ai-sdlc-append-only-conflict-policy.md` 통합
   - `operations/toc.yml`: ai-sdlc 항목 제거 → bridge 문서 `ai-sdlc.md`로 대체
4. `Docs/docfx.json` metadata에 `PlatformA.SdlcDB.Lib` 추가
5. `.github/scripts/generate_ai_sdlc_docs.py` 신규 작성
   - `skill-reference.md`: `.claude/skills/*/SKILL.md` 파싱 → 스킬 테이블
   - `automation-map.md`: `.github/workflows/*.yml` 파싱 → workflow 테이블
   - `gates.md`: done/pr SKILL.md에서 gate 플래그 + DB 판정 기준 추출
6. `.github/workflows/docs.yml`에 `generate_ai_sdlc_docs.py` 단계 추가 (`generate_db_schema.py` 다음)
7. `.claude/skills/doc-writer/SKILL.md`에 `ai-sdlc` 섹션 추가
8. `.github/scripts/check_docs_toc.py` 신규 작성 (toc 미등재 파일 감지)
9. DocFX 로컬 빌드 검증 (`docfx Docs/docfx.json`, 0 error, InvalidFileLink 없음)

## 영향 범위 (예상)

| 파일/디렉토리 | 변경 유형 |
|---|---|
| `Docs/toc.yml` | 수정 |
| `Docs/docfx.json` | 수정 |
| `Docs/ai-sdlc/` | 신규 디렉토리 (15개 파일) |
| `Docs/operations/ai-sdlc-*.md` 6개 | 이동 (삭제 후 신규 위치에 생성) |
| `Docs/operations/ai-sdlc.md` | 신규 (bridge 문서) |
| `Docs/operations/toc.yml` | 수정 |
| `.github/scripts/generate_ai_sdlc_docs.py` | 신규 |
| `.github/scripts/check_docs_toc.py` | 신규 |
| `.github/workflows/docs.yml` | 수정 (1줄 추가) |
| `.claude/skills/doc-writer/SKILL.md` | 수정 |

## 제약 및 주의사항

- C# 코드 변경 없음 — `dotnet build`/`dotnet test`는 기존 통과 상태 유지
- 이동되는 파일의 기존 경로 링크 깨짐 방지 → `operations/ai-sdlc.md` bridge 문서 필수
- `Docs/ai-sdlc/toc.yml`의 모든 href 파일이 실제로 존재해야 DocFX 빌드 성공
- `SdlcDB.Lib` DocFX 추가 시 빌드 오류 가능 → TASK 9에서 로컬 검증 필수
- ADR-009(PostgreSQL SDLC DB): SdlcDB.Lib는 ADR-009에서 결정된 구조 그대로 문서화

## 구현 접근 방향

1. **TASK 1→4**: toc.yml, docfx.json 수정 먼저 → 이후 Docs/ai-sdlc/ 파일이 생기면 DocFX 빌드 가능
2. **TASK 3**: 파일 이동은 `Read` 후 `Write` + 원본 삭제(Bash mv) 순서로 진행
3. **TASK 5**: `generate_redis_key_docs.py` 패턴 참조 (marker 기반 갱신)
4. **TASK 8**: `check_docs_toc.py`는 exit 0/1로 경고, docs.yml에서 `|| true`로 비차단
5. **TASK 9**: DocFX CLI가 로컬에 설치되어 있으면 실행, 없으면 CI에서 검증

## 검증 기준

1. `docfx Docs/docfx.json` → Build succeeded, 0 error(s), InvalidFileLink 없음
2. `python .github/scripts/check_docs_toc.py` → 미등재 파일 없음
3. `python .github/scripts/generate_ai_sdlc_docs.py` → gates/skill-reference/automation-map 갱신
4. `cd PlatformA && dotnet build PlatformA.sln && dotnet test PlatformA.sln` → 현재 통과 상태 유지
5. DocFX 로컬 또는 CI에서 AI SDLC 섹션 좌측 메뉴 노출 확인
