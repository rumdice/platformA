# AI_SDLC Sprints

이 디렉터리는 AI_SDLC 스프린트별 상세 작업 파일을 보관한다.

## 목적

기존 `AI/SPRINT.md`는 append-only 구조라서 같은 날짜에 여러 브랜치가 생성될 경우
merge conflict가 자주 발생한다.

이를 완화하기 위해 스프린트별 상세 내용은 개별 파일로 분리한다.

## 구조

- `AI/SPRINT.md` — 스프린트 인덱스/요약 (Active Sprint 파일 경로 포함)
- `AI/sprints/sprint-NNN.md` — 개별 스프린트 상세

## 명명 규칙

```
sprint-NNN.md    NNN = 3자리 zero-padded 스프린트 번호
```

## 새 스프린트 작성 방법

1. `AI/sprints/sprint-NNN.md` 파일 생성 (이 디렉터리에)
2. `AI/SPRINT.md`의 Active Sprint 테이블에 경로 등록

기존 스프린트 #1~#48의 상세는 `AI/SPRINT.md`에 직접 기록되어 있으며 보존된다.
스프린트 #49부터 이 디렉터리를 사용한다.
