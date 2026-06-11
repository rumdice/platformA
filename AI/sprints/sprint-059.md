---
sprint: 59
title: DocFX AI_SDLC 문서 섹션 분리
branch: 2026-06-11_ImproveDocFxAiSdlcDocs
date: 2026-06-11
status: in-progress
---

# Sprint #59 — DocFX AI_SDLC 문서 섹션 분리

## 목표

AI_SDLC 문서를 DocFX 최상위 섹션으로 분리하여 "서버 플랫폼 문서"와 "AI 개발 자동화 시스템 문서"를 명확히 구분한다.

## 태스크

- [ ] `Docs/toc.yml` — AI SDLC 최상위 섹션 추가
- [ ] `Docs/ai-sdlc/` 디렉토리 신설 (toc.yml + 15개 문서)
- [ ] `Docs/operations/ai-sdlc-*.md` 6개 → `Docs/ai-sdlc/`로 이동
- [ ] `Docs/docfx.json` — PlatformA.SdlcDB.Lib metadata 추가
- [ ] `.github/scripts/generate_ai_sdlc_docs.py` 신규 작성
- [ ] `.github/workflows/docs.yml` — AI SDLC 문서 생성 단계 추가
- [ ] `.claude/skills/doc-writer/SKILL.md` — ai-sdlc 섹션 추가
- [ ] `.github/scripts/check_docs_toc.py` 신규 작성
- [ ] DocFX 로컬 빌드 검증 (0 error, InvalidFileLink 없음)

## 배경

AI_SDLC 6개 정책 문서가 `Docs/operations/`에 분산되어 있고 5개가 toc.yml에 미등재.
AI_SDLC는 단순 운영 기능이 아닌 PlatformA 생산 시스템이므로 별도 섹션으로 승격 필요.

## 참조

- DB job: `sdlc.ai_jobs.branch = 2026-06-11_ImproveDocFxAiSdlcDocs`
- 선행 스프린트: #58(AddSdlcWorkflowTests), #55(Phase C 경화)
- 계획 파일: `PLAN_2026-06-11_PlatformA_DocFX_AI_SDLC_Docs_Improvement.md`
