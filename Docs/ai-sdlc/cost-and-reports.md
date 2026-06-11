# AI_SDLC Cost & Reports

AI 작업 비용 추적 및 리포트 생성 가이드.

## 데이터 흐름

```
/pr 스킬
  └─ count_tokens.py        ← Claude API 토큰 사용량 계산
  └─ db_write.py upsert-job ← ai_jobs.consume_tokens / cache_tokens 업데이트
  └─ insert_model_run.py    ← ai_model_runs 레코드 삽입
  └─ generate_cost_log_from_db.py ← AI/reports/generated-cost-log-from-db.md 생성
```

Phase C 이후 `AI/cost-log.md` 직접 append는 중단됐다.
모든 비용 데이터는 PostgreSQL `sdlc.ai_model_runs`가 단일 진실 공급원이다.

## 스크립트 사용법

### generate_cost_log_from_db.py

DB에서 전체 작업 비용을 집계하여 markdown 리포트를 생성한다.

```bash
# 기본 출력
python .github/scripts/generate_cost_log_from_db.py \
  --output AI/reports/generated-cost-log-from-db.md

# 특정 스프린트만
python .github/scripts/generate_cost_log_from_db.py \
  --sprint 59 \
  --output AI/reports/sprint-059-cost.md

# 날짜 범위
python .github/scripts/generate_cost_log_from_db.py \
  --from 2026-06-01 \
  --to 2026-06-30
```

### count_tokens.py

Claude API 사용 내역을 집계한다. `/pr` 스킬에서 자동 호출된다.

```bash
python .github/scripts/count_tokens.py "2026-06-11T00:00:00Z"
# 출력: duration_sec=120, consume_tokens=45000, cache_tokens=120000
```

### insert_model_run.py

`sdlc.ai_model_runs` 레코드를 삽입한다. `/pr` 스킬에서 자동 호출된다.

```bash
python .github/scripts/insert_model_run.py \
  --branch my-branch \
  --created-at "2026-06-11T00:00:00Z"
```

## DB 조회

```sql
-- 스프린트별 비용 집계
SELECT j.sprint, j.task_name, j.branch,
       j.consume_tokens, j.cache_tokens, j.duration_sec,
       j.status, j.pr_url
FROM sdlc.ai_jobs j
ORDER BY j.sprint DESC, j.created_at DESC;

-- 전체 모델 사용량
SELECT model_name, provider,
       SUM(input_tokens) AS total_input,
       SUM(output_tokens) AS total_output,
       SUM(total_tokens) AS total_tokens,
       SUM(estimated_cost) AS total_cost
FROM sdlc.ai_model_runs
GROUP BY model_name, provider
ORDER BY total_cost DESC;

-- 이번 달 작업 요약
SELECT DATE_TRUNC('day', created_at) AS day,
       COUNT(*) AS jobs,
       SUM(consume_tokens) AS tokens
FROM sdlc.ai_jobs
WHERE created_at >= DATE_TRUNC('month', NOW())
GROUP BY day ORDER BY day DESC;
```

## 생성된 리포트 위치

| 파일 | 내용 |
|------|------|
| `AI/reports/generated-cost-log-from-db.md` | 전체 작업 비용 (PR 생성 시 자동 갱신) |
| `AI/cost-log.md` | 읽기 전용 아카이브 (Phase C 이전 데이터) |

## 관련 문서

- [DB Schema](db-schema.md)
- [Phase C DB 단독 운영](phase-c-db-only.md)
