# AI_SDLC PostgreSQL Backup / Restore

`platforma_sdlc` DB 백업·복원 절차.

## 자동 백업

### backup_sdlc_db.sh

매일 자동 실행 (cron 또는 수동):

```bash
bash .github/scripts/backup_sdlc_db.sh
```

- 저장 위치: `AI/backups/sdlc_db_YYYY-MM-DD.sql.gz`
- 보관 기간: 7일 (자동 삭제)
- 포함 내용: `sdlc` 스키마 전체 (ai_jobs, ai_job_steps, ai_model_runs, ai_failures, sprint_seq)

### 스케줄 설정 예시 (Windows Task Scheduler)

```
트리거: 매일 오전 3:00
작업: bash C:\Users\rumdi\Desktop\workspace\platformA\.github\scripts\backup_sdlc_db.sh
```

## 수동 백업

```bash
pg_dump \
  --host localhost \
  --port 5432 \
  --username platforma \
  --schema sdlc \
  --no-password \
  platforma_sdlc \
  | gzip > AI/backups/sdlc_db_manual_$(date +%Y-%m-%d_%H%M).sql.gz
```

환경변수로 비밀번호 설정: `PGPASSWORD=platforma_dev_password`

## 복원

```bash
# 복원 대상 DB가 존재하는 경우 (덮어쓰기)
gunzip -c AI/backups/sdlc_db_YYYY-MM-DD.sql.gz | \
  psql \
    --host localhost \
    --port 5432 \
    --username platforma \
    platforma_sdlc

# 신규 DB에 복원 (최초 설치 후)
createdb -U platforma platforma_sdlc
gunzip -c AI/backups/sdlc_db_YYYY-MM-DD.sql.gz | \
  psql -U platforma platforma_sdlc
```

## 마이그레이션 재적용

DB를 새로 만들거나 스키마가 누락된 경우:

```bash
cd PlatformA/PlatformA.SdlcDB.Lib
dotnet ef database update --context SdlcDbContext
```

## 백업 상태 확인

```bash
ls -la AI/backups/*.sql.gz
```

## 정합성 검사

복원 후 정합성 확인:

```bash
python .github/scripts/check_sdlc_consistency.py --strict
```

exit 0이면 정상.

## Docker PostgreSQL 컨테이너 사용 시

```bash
# 컨테이너 이름 확인
docker ps | grep sdlc-postgres

# 컨테이너 내부에서 pg_dump
docker exec sdlc-postgres pg_dump \
  -U platforma \
  -n sdlc \
  platforma_sdlc | gzip > AI/backups/sdlc_db_$(date +%Y-%m-%d).sql.gz
```

## 관련 문서

- [DB Schema](db-schema.md)
- [Phase C DB 단독 운영](phase-c-db-only.md)
