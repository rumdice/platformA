# 요구사항 명세: AddLlmRouter

작성일: 2026-06-05
브랜치: 2026-06-05_AddLlmRouter
소스: task JSON summary + SPRINT.md

## 요구사항 요약

task JSON의 `impact.risk`(LOW/MEDIUM/HIGH)에 따라 `claude --model` 플래그를 자동 선택하는
LLM 라우터를 구현한다. GitHub Actions 워크플로우와 /workflow SKILL.md에 적용하여 불필요하게
비싼 모델 사용을 방지한다.

## 상세 요구사항

1. **`get_task_risk.py` 신규 작성** (`.github/scripts/`)
   - 인수: `--branch <브랜치명>` (필수)
   - AI/tasks/ 디렉토리에서 브랜치에 해당하는 task JSON을 탐색
   - `impact.risk` 필드를 읽어 아래 매핑으로 모델명 출력:
     - LOW → `claude-haiku-4-5-20251001`
     - MEDIUM (기본값) → `claude-sonnet-4-6`
     - HIGH → `claude-opus-4-8`
   - task JSON이 없거나 impact가 null이면 기본값 MEDIUM(sonnet) 사용
   - 출력 형식: `MODEL=claude-sonnet-4-6` (환경변수 형식)
   - Python 3.9 호환 (`Optional` 타입 힌트)

2. **`auto-fix.yml` 수정**
   - /qa-failure 실행 단계 이전에 모델 선택 단계 추가
   - `failure_type` 기반 매핑:
     - FORMAT → `claude-haiku-4-5-20251001` (단순 포맷 수정)
     - BUILD, TEST → `claude-sonnet-4-6` (빌드·테스트 분석)
     - 그 외 → `claude-sonnet-4-6` (기본값)
   - `$MODEL` 환경변수를 claude CLI 호출 시 `--model $MODEL`로 전달

3. **`plan-file-trigger.yml` 수정**
   - 계획 파일 탐지 단계 이후 `risk:` 힌트 파싱 단계 추가
   - 계획 파일 첫 10줄에서 `risk: LOW|MEDIUM|HIGH` 형태의 줄 검색
   - 매핑:
     - risk: LOW → `claude-haiku-4-5-20251001`
     - risk: HIGH → `claude-opus-4-8`
     - risk: MEDIUM 또는 힌트 없음 → `claude-sonnet-4-6` (기본값)
   - `$MODEL` 변수를 /workflow 실행 시 `--model $MODEL`로 전달

4. **`/workflow` SKILL.md 수정**
   - /impact 완료(3단계) 직후에 "3.5단계: LLM 모델 선택" 추가
   - get_task_risk.py 호출하여 `MODEL` 변수 설정
   - 이후 단계 설명에 "(MODEL 변수 참조)" 노트 추가
   - 실제 대화형 세션에서는 모델 변경 불가 — 정보 출력 목적으로만 사용

## 영향 범위 (예상)

- `.github/scripts/get_task_risk.py` — 신규 (Python, ~50줄)
- `.github/workflows/auto-fix.yml` — 모델 선택 단계 추가 (~10줄)
- `.github/workflows/plan-file-trigger.yml` — risk 파싱 + 모델 선택 추가 (~15줄)
- `.claude/skills/workflow/SKILL.md` — 3.5단계 추가 (~10줄)
- C# 코드 변경 없음

## 제약 및 주의사항

- ADR-009: SDLC 자동화 범위 내 — 신규 ADR 불필요
- Python 3.9 호환 필수 (union 타입 힌트 사용 금지)
- claude CLI `--model` 플래그: 인터랙티브 세션에서는 현재 세션 모델 변경 불가 — GitHub Actions에서만 유효
- `--dangerously-skip-permissions` 플래그는 기존과 동일하게 유지
- MODEL 변수가 빈 경우 기본값 `claude-sonnet-4-6` 사용

## 구현 접근 방향

1. get_task_risk.py: AI/tasks/ glob → branch 매칭 → json.load → impact.risk → 모델명 출력
2. auto-fix.yml: `if/elif` shell 분기로 FAILURE_TYPE → MODEL 설정 → GITHUB_ENV에 저장
3. plan-file-trigger.yml: grep으로 plan 파일에서 risk 라인 추출 → MODEL 결정 → GITHUB_ENV에 저장
4. workflow SKILL.md: 기존 3단계(/impact) 후 3.5단계 추가

## 검증 기준

- `python .github/scripts/get_task_risk.py --branch 2026-06-05_AddLlmRouter` 실행 시
  `MODEL=claude-sonnet-4-6` (또는 해당 브랜치 risk에 맞는 값) 출력
- auto-fix.yml: failure_type=FORMAT이면 haiku 모델 사용하도록 명시
- plan-file-trigger.yml: `risk: LOW` 라인이 있는 계획 파일 push 시 haiku 모델로 claude 실행
- 빌드 오류 없음 (C# 변경 없으므로 기존 133개 테스트 통과 유지)
