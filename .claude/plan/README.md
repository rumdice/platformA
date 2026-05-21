# .claude/plan/ — 외부 계획 파일 제출 디렉토리

이 디렉토리는 **외부에서 작성한 계획 파일**을 파이프라인에 제출하기 위한 곳이다.

## 사용 방법

1. 외부 에이전트, 기획서, 노션 등에서 작성한 계획을 `.md` 파일로 이 디렉토리에 저장한다.
2. `/requirement` 를 인수 없이 실행하면 이 디렉토리의 파일을 자동으로 읽어 분석한다.
3. 처리 완료된 파일은 `processed/` 하위 디렉토리로 자동 이동된다.

## 파일명 규칙

자유롭게 지정 가능 (처리 시 `YYYY-MM-DD_NNN_PlanName.md` 형식으로 변환 저장됨)

```
예) temp_plan.md
    feature-login-improvement.md
    sprint23-tasks.md
```

## 입력 우선순위 (`/requirement` 실행 시)

```
1순위: .claude/plan/*.md       (이 디렉토리의 파일 — 외부 제출 계획)
2순위: ~/.claude/plans/        (plan mode 파일 — aws-wiggly-bee.md 등)
3순위: /requirement [텍스트]   (직접 입력)
```

## processed/ 디렉토리

처리 완료된 파일이 자동으로 이동된다. 이력 확인 용도로 보존된다.
