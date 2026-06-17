# 요구사항 명세: AddSdlcPyUnitTestCi

작성일: 2026-06-18
브랜치: 2026-06-18_AddSdlcPyUnitTestCi
소스: plan mode (hazy-booping-moore.md)

## 요구사항 요약

Sprint #64(PR #96)에서 추가한 `.github/tests/` Python 유닛 테스트 2개(25 tests)가
기존 `sdlc-python-test.yml`의 실행 범위에 포함되지 않아 CI에서 실행되지 않는 문제를 수정한다.

## 상세 요구사항

1. `sdlc-python-test.yml`의 `paths:` 트리거에 `.github/tests/**`를 추가한다.
   - `.github/tests/` 파일이 변경될 때 워크플로우가 자동 트리거되어야 한다.
2. 기존 통합 테스트 step 이전에 유닛 테스트 실행 step을 추가한다.
   - 명령: `python -m pytest .github/tests/ -v --tb=short`
   - `-m integration` 마커 없음 (unittest.TestCase 기반, DB 불필요)
3. 기존 통합 테스트 step(`.github/scripts/tests/ -m integration`)은 그대로 유지한다.

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|------|---------|
| `.github/workflows/sdlc-python-test.yml` | 수정 — paths 1줄 추가, step 3줄 추가 |

## 제약 및 주의사항

- 기존 통합 테스트 step을 변경하지 않는다 (DB 연결이 필요한 테스트는 별도 유지)
- 유닛 테스트는 DB 없이 실행 가능 — GitHub Actions runner에서 항상 통과해야 함
- `record_failure.py`, `count_tokens.py`가 `.github/scripts/`에 있어야 import 가능
  → `sys.path.insert(0, ...)` 패턴이 이미 테스트 파일에 구현되어 있음 (확인 필요)

## 구현 접근 방향

`sdlc-python-test.yml`을 두 곳만 수정한다:

```yaml
# 1. paths: 트리거에 추가
- '.github/tests/**'

# 2. 기존 통합 테스트 step 앞에 유닛 테스트 step 추가
- name: Run Python unit tests (.github/tests/)
  run: |
    python -m pytest .github/tests/ -v --tb=short
```

## 검증 기준

- `python -m pytest .github/tests/ -v --tb=short` 로컬 실행 시 25 passed 출력
- PR push 후 GitHub Actions `AI_SDLC Python Integration Tests` 워크플로우에서
  "Run Python unit tests" step이 실행되어 25 passed 확인
- 기존 통합 테스트 step도 이상 없이 실행됨
