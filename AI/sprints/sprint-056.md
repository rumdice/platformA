---
sprint: 56
title: SPRINT.md 자동 재생성 — 동시 개발 충돌 해소 (Option B)
branch: 2026-06-10_SprintMdAutoGen
date: 2026-06-10
status: in-progress
---

# Sprint #56 — SPRINT.md 자동 재생성 (Option B)

## 목표

1인 다수 agent / 다인 팀 동시 개발 시 발생하는 SPRINT.md 머지 충돌을 완전히 해소한다.
에이전트가 AI/SPRINT.md를 직접 수정하지 않고, PR 머지 후 generate_sprint_md.py가 DB 기반으로 자동 재생성한다.

## 태스크

- [x] `generate_sprint_md.py` 신규 작성 — DB 우선, 파일 폴백, Active/Recent 테이블 재생성
- [x] `/plan` SKILL.md — SPRINT.md 수정 제거, sprint-NNN.md YAML 프론트매터 템플릿 추가
- [x] `/pr` SKILL.md — SPRINT.md 수정 제거, sprint-NNN.md 완료 상태 갱신으로 대체
- [x] `pr-merge-sync.yml` — `generate_sprint_md.py` 호출 추가, AI/sprints/ git add 포함
- [x] `session-start.sh` — DB list-active 기반 SPRINT 현황 출력으로 전환
- [x] `AI/SPRINT.md` — 헤더 설명 업데이트, 초기 DB 재생성 실행

## 배경

Phase C 완성도 체크에서 발견된 Git 레이어 충돌 갭 (완성도 75%).
두 에이전트가 동시에 `/plan`을 실행하면 SPRINT.md Active Sprint 테이블을 각자 수정
→ PR 머지 시 충돌 발생. Option B(GitHub Actions 자동 재생성)로 완전 해소.

## 참조

- DB job: `sdlc.ai_jobs.branch = 2026-06-10_SprintMdAutoGen`
- 선행 분석: Phase C 동시 개발 완성도 체크 (2026-06-10)
