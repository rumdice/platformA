# AI SDLC Workflow

## 전체 파이프라인

```
사용자 입력
    │
    ▼
/workflow 작업설명  ←── 또는 .claude/plan/*.md 파일 제공
    │
    ├─ 1. /plan        브랜치 생성 + DB ai_jobs 초기화 + sprint-NNN.md 생성
    ├─ 2. /requirement 요구사항 명세 파일 생성 + DESIGN_REVIEW
    ├─ 3. /impact      영향 범위 분석 (코드 수정 전)
    ├─ 4. /start       Job Lock claim + 명세 파일 읽기 + 작업 지시서 출력
    ├─ 5. (코딩)        Claude가 코드 직접 수정
    ├─ 6. /test-gen    변경 파일 기반 테스트 케이스 생성
    ├─ 7. /done        빌드·포맷·테스트 검증 → push
    ├─ 8. /review      패턴·보안·아키텍처 리뷰
    └─ 9. /pr          sprint-NNN.md 완료 처리 + PR 생성 + 비용 기록
            │
            ▼
    사람이 PR 검토 후 머지
            │
            ▼
    CI — Build & Test → Docs 자동 재생성 → GitHub Pages 배포
```

## 스킬 역할

| 스킬 | 단계 | 주요 산출물 |
|---|---|---|
| `/plan` | Stage 1 | 브랜치, DB ai_jobs, sprint-NNN.md |
| `/requirement` | Stage 2 | 명세 파일 (.claude/plan/), DESIGN_REVIEW |
| `/impact` | 사전 분석 | 위험도 판정, DB impact 기록 |
| `/start` | 코딩 진입 | Job Lock claim, 작업 지시서 |
| `/test-gen` | 테스트 | xUnit 테스트 파일 |
| `/done` | 검증 | 빌드·테스트 통과 확인, push |
| `/review` | 리뷰 | 패턴·보안 체크리스트 결과 |
| `/pr` | 완료 | PR URL, 비용 기록, Job Lock release |

## 게이트 검사 흐름

`/done`과 `/pr`은 push 전·PR 생성 전에 아래 게이트를 DB에서 확인합니다:

```
requirement_done  ←─ /requirement 완료 시 DB에 기록
impact_done       ←─ /impact 완료 시 DB에 기록
test_generated    ←─ /test-gen 완료 시 DB에 기록
review_completed  ←─ /review 완료 시 DB에 기록
adr_required      ←─ /requirement의 DESIGN_REVIEW가 ADR 필요 판정 시 true
```

모든 게이트가 통과해야 `/pr`이 PR을 생성합니다.

## 단독 실행 (수동 워크플로)

완전 자동화가 아닌 단계별 실행도 가능합니다:

```bash
/plan 작업설명    # Stage 1
/requirement      # Stage 2
/impact           # 사전 분석
/start            # 코딩 시작
# (코딩)
/test-gen         # 테스트 생성
/done             # 검증 및 push
/review           # 리뷰
/pr               # PR 생성
```
