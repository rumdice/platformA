---
sprint: 65
title: sdlc-python-test CI 유닛 테스트 경로 추가
branch: 2026-06-18_AddSdlcPyUnitTestCi
date: 2026-06-18
status: in-progress
---

# Sprint #65 — sdlc-python-test CI 유닛 테스트 경로 추가

## 목표

`sdlc-python-test.yml`이 Sprint #64에서 추가한 `.github/tests/` 유닛 테스트를 CI에서 실행하도록 수정한다.

## 태스크

- [ ] `.github/workflows/sdlc-python-test.yml` — `paths:` 트리거에 `.github/tests/**` 추가
- [ ] `.github/workflows/sdlc-python-test.yml` — 유닛 테스트 실행 step 추가 (`python -m pytest .github/tests/`)

## 배경

Sprint #64(PR #96)에서 Python 유닛 테스트 2개를 추가했으나:
- `test_record_failure.py` (16 tests), `test_count_tokens.py` (9 tests)
- 기존 `sdlc-python-test.yml`은 `.github/scripts/tests/`만 실행 → `.github/tests/`는 CI에서 누락됨

## 참조

- DB job: `sdlc.ai_jobs.branch = 2026-06-18_AddSdlcPyUnitTestCi`
- 선행 스프린트: Sprint #64 (PR #96)
