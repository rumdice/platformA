---
sprint: 60
title: sync_merged_pr Phase C 오탐 수정
branch: 2026-06-11_FixSyncMergedPrPhaseC
date: 2026-06-11
status: done
completed: 2026-06-11
pr: https://github.com/rumdice/platformA/pull/90
---

# Sprint #60 — sync_merged_pr Phase C 오탐 수정

## 목표

`sync_merged_pr.py`가 task JSON 부재 시 무조건 경고를 발생시키는 오탐을 수정한다. Phase C 브랜치는 DB `sdlc.ai_jobs`에 job이 있으면 경고를 건너뛴다.

## 태스크

- [x] `sync_merged_pr.py` — `find_task_file` 실패 시 `AI/sprints/*.md` frontmatter 탐색 추가
- [x] sprint 파일에 branch가 있으면 경고 코멘트 스킵 (Phase C 정상 경로)
- [x] sprint 파일도 없을 때만 기존 경고 유지 (진짜 SDLC 누락 케이스)

## 배경

Phase C(2026-06-10~)부터 task JSON 신규 생성 중단 → 모든 Phase C 브랜치에서 PR 머지 시
`⚠️ task JSON 없음` 경고가 오탐으로 발생. PR #89(ImproveDocFxAiSdlcDocs)에서 처음 확인.

## 참조

- DB job: `sdlc.ai_jobs.branch = 2026-06-11_FixSyncMergedPrPhaseC`
- 원인 파일: `.github/scripts/sync_merged_pr.py`
- 관련 워크플로: `.github/workflows/pr-merge-sync.yml`
