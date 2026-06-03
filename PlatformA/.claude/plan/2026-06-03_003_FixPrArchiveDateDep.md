# 요구사항 명세: FixPrArchiveDateDep

작성일: 2026-06-03
브랜치: 2026-06-03_FixPrArchiveDateDep
소스: 사용자 요청

## 요구사항 요약

`/pr` 스킬 4.5단계에서 명세 파일을 `processed/`로 이동할 때 `TODAY` 날짜로만 검색하는 방식을
`PlanName`으로만 검색하도록 수정하여, `/requirement`와 `/pr`을 다른 날 실행해도 파일이
정상적으로 아카이브되도록 한다.

## 상세 요구사항

1. `.claude/skills/pr/SKILL.md` 4.5단계의 `SPEC_FILE` 검색 패턴 변경
   - 변경 전: `ls .claude/plan/${TODAY}_*_${PLAN_NAME}.md`
   - 변경 후: `ls .claude/plan/*_${PLAN_NAME}.md`
2. 변경 후에도 `processed/` 폴더 내 파일과 충돌하지 않아야 함
   (`.claude/plan/` 만 검색하며 `processed/` 서브디렉토리는 포함되지 않음 — glob 패턴 특성상 자동 제외)

## 영향 범위

| 파일 | 유형 | 위험도 |
|------|------|--------|
| `.claude/skills/pr/SKILL.md` | 수정 (1줄) | 🟢 LOW |

## 제약 및 주의사항

- `processed/` 하위 파일이 검색에 포함되지 않아야 함 → `ls .claude/plan/*_${PLAN_NAME}.md`는 하위 디렉토리를 탐색하지 않으므로 안전
- 같은 PlanName을 가진 오래된 spec 파일이 남아있을 경우 `head -1`로 첫 번째 파일만 선택

## 검증 기준

- SKILL.md 수정 후 패턴 변경 내용 확인
- 다른 날짜 명세 파일도 PlanName이 일치하면 `processed/`로 이동됨
