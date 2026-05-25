# AI/reports

AI_SDLC 작업 현황 리포트를 저장하는 디렉토리.

## 리포트 종류

| 파일명 | 설명 |
|--------|------|
| `weekly_YYYY-WNN.md` | 주간 SDLC 리포트 |

## 생성 방법

```bash
python3 .github/scripts/generate_sdlc_report.py
```

프로젝트 루트에서 실행한다. `AI/tasks/*.json`과 `AI/cost-log.md`를 읽어 해당 주차 리포트를 생성한다.

## 집계 항목

- 전체/완료/실패/진행 중 task 수
- 작업 규모 분포 (S/M/L/XL, cost-log 기준)
- `test_generated == false`인 완료 task 목록
- `review_completed == false`인 HIGH risk 완료 task 목록
- `impact == null`인 코드 변경 task 목록
- 완료 task 전체 목록 (PR 링크 포함)

## 자동화 예정

현재는 수동 실행만 지원한다. 향후 GitHub Actions 스케줄 또는 PR merge 시 자동 생성을 검토한다.
