---
sprint: 89
title: 아키텍처 문서 자동화
branch: 2026-07-09_AutoArchitectureDocs
date: 2026-07-09
status: done
completed: 2026-07-09
---

# Sprint #89 — 아키텍처 문서 자동화

## 목표
launchSettings.json·Program.cs·Hub 파일을 파싱하여 overview.md를 자동 재생성하는 스크립트를 작성하고, docs.yml에 연결하여 main 머지 시 아키텍처 문서가 자동 갱신되도록 한다.

## 태스크
- [x] .github/scripts/generate_architecture_docs.py 작성 (파싱 + overview.md 재생성)
- [x] docs.yml에 "Generate architecture docs" 스텝 추가
- [x] overview.md를 스크립트로 1회 재생성 (Game.Lobby/Gomoku 반영)
- [x] sequences.md Section 3 매칭 시퀀스 현행화 수동 갱신

## 배경
Game.Lobby(SignalR :7777), Game.Gomoku(TCP :7778) 서비스 추가 및 매칭 흐름 변경(Client→Lobby→Matching 내부 HTTP)이 여러 스프린트에 걸쳐 이루어졌으나 overview.md·sequences.md가 수동 관리 문서라 업데이트가 누락됨.
generate_architecture_docs.py 스크립트가 main 머지 시마다 자동 재생성하여 stale 상태를 방지한다.

## 참조
- DB job: `sdlc.ai_jobs.branch = 2026-07-09_AutoArchitectureDocs`
