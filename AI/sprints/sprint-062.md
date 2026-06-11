---
sprint: 62
title: Phase C 참조 정리
branch: 2026-06-12_CleanupPhaseCReferences
date: 2026-06-12
status: in-progress
---

# Sprint #62 — Phase C 참조 정리

## 목표

AI/SPRINT.md·AI/cost-log.md 삭제(Phase C) 이후 해당 파일을 직접 참조하는 스킬·워크플로·문서를 정리하여 /sprint·/workreport가 정상 동작하고 pr-merge-sync CI 경고를 제거한다.

## 태스크

- [x] `.claude/commands/sprint.md` — AI/SPRINT.md 읽기 제거, DB+sprints 폴백으로 교체
- [x] `.claude/skills/workreport/SKILL.md` — sidecar `tail AI/SPRINT.md` 제거, `grep cost-log.md` 제거, DB/sprints 기반 데이터 수집으로 교체
- [x] `.github/workflows/pr-merge-sync.yml` — `git add AI/SPRINT.md AI/cost-log.md` 제거, 커밋 메시지 수정, 스텝명 수정
- [x] `Docs/operations/ai-sdlc-*.md` 6개 삭제 — 내용이 Docs/ai-sdlc/로 이동 완료된 고아 파일
- [x] `session-start.sh` 죽은 변수 `SPRINT_FILE` 제거

## 배경

2026-06-11 PR #92로 AI/SPRINT.md, AI/cost-log.md, Docs/operations/ai-sdlc.md가 삭제됨.
그러나 이를 직접 참조하는 코드가 4개 파일에 남아:
- /sprint 커맨드 실행 시 빈 결과
- /workreport 스킬 로드 시 sidecar 오류 (어제 실패 확인)
- pr-merge-sync.yml이 삭제된 파일 git add → CI 경고
- Docs/operations/ 6개 고아 파일 → check_docs_toc.py 경고

## 참조

- DB job: `sdlc.ai_jobs.branch = 2026-06-12_CleanupPhaseCReferences`
- 관련 PR: #92 (파일 삭제), #89 (ai-sdlc 섹션 신설)
- 분석 파일: `.claude/commands/sprint.md`, `.claude/skills/workreport/SKILL.md`, `.github/workflows/pr-merge-sync.yml`
