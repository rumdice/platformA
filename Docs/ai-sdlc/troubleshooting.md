# AI_SDLC Troubleshooting

자주 발생하는 문제와 해결 방법.

## PostgreSQL 연결 실패

### 증상
```
DB 게이트 조회 실패. PostgreSQL 연결을 확인하세요.
```

### 원인 및 해결

```bash
# Docker 컨테이너 실행 여부 확인
docker ps | grep sdlc-postgres

# 미실행 시 재기동
cd docker/postgresql && docker-compose up -d

# 직접 연결 테스트
psql -h localhost -p 5432 -U platforma -d platforma_sdlc -c "SELECT 1"
```

환경변수 확인:
```bash
echo $SDLC_DB_CONNECTION
# 없으면 기본값 사용: Host=localhost;Port=5432;Database=platforma_sdlc;Username=platforma;Password=platforma_dev_password
```

---

## Job Lock 획득 실패

### 증상
```
⛔ AI_SDLC job lock 획득 실패 — 다른 agent가 이미 작업 중일 수 있습니다.
```

### 원인 및 해결

```bash
# 현재 lock 상태 확인
python .github/scripts/job_lock.py status --branch my-branch

# lock 소유자 확인
python .github/scripts/check_sdlc_consistency.py
# → active_locks, stale_locks 항목 확인

# stale lock 수동 해제 (TTL 만료 확인 후)
python .github/scripts/job_lock.py expire
```

세션 시작 시 `[AI_SDLC Job Lock]` 섹션에서도 확인 가능.

---

## SDLC gate 검사 차단

### 증상
```
❌ /done 중단: /test-gen이 실행되지 않았습니다.
```

### 해결

```bash
# 해당 스킬 실행
/test-gen   # test_generated 미충족 시
/impact     # impact_done 미충족 시
/review     # review_completed 미충족 시
/requirement # requirement_done 미충족 시
```

테스트 파일이 이미 충분하여 test-gen이 불필요한 경우:
```bash
python .github/scripts/db_write.py \
  --action upsert-job \
  --branch my-branch \
  --test-generated 2>/dev/null
```

---

## DB write 실패 로그

### 확인 방법

```bash
# 오늘 실패 로그
cat AI/logs/db-write-failures/$(date +%Y-%m-%d).log
```

세션 시작 시 `[AI_SDLC DB Write 실패 감지]` 섹션에도 표시된다.

### 흔한 오류

| 오류 | 해결 |
|------|------|
| `null value in column "task_name"` | `--task` 인수 누락. db_write.py 호출 스킬 확인 |
| `duplicate key value violates unique constraint "ai_jobs_branch_key"` | 동일 브랜치로 재실행. `--action upsert-job` 사용 중인지 확인 |
| `relation "sdlc.ai_jobs" does not exist` | Migration 미적용. `dotnet ef database update` 실행 |

---

## DocFX 빌드 오류

### InvalidFileLink

```
InvalidFileLink: Docs/ai-sdlc/some-file.md
```

→ `Docs/ai-sdlc/toc.yml`에 href가 있지만 파일이 없는 경우.
`check_docs_toc.py`로 미등재 파일 확인:
```bash
python .github/scripts/check_docs_toc.py
```

### SdlcDB.Lib 빌드 오류

```
Could not find project PlatformA.SdlcDB.Lib.csproj
```

→ `Docs/docfx.json`의 경로 확인:
```json
"../PlatformA.SdlcDB.Lib/PlatformA.SdlcDB.Lib.csproj"
```

---

## 정합성 검사 실패

```bash
python .github/scripts/check_sdlc_consistency.py --strict
```

### unmatched_files (DB에 없는 task JSON)

Phase C 이전 파일이 DB에 마이그레이션되지 않은 경우:
```bash
python .github/scripts/migrate_tasks_to_postgres.py --apply
```

### gate_mismatch (DB와 파일 값 불일치)

Phase B에서 발생. Phase C에서는 파일 write 중단으로 더 이상 발생하지 않는다.

---

## n8n 관련

[n8n 트러블슈팅](n8n.md#11-트러블슈팅) 참조.

---

## 빌드 오류 (C# 솔루션)

```bash
# 빌드 캐시 오류 (MSB3492)
cd PlatformA && dotnet clean PlatformA.sln && dotnet build PlatformA.sln
```
