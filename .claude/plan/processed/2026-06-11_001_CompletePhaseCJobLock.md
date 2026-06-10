# 요구사항 명세: CompletePhaseCJobLock

작성일: 2026-06-11
브랜치: 2026-06-11_CompletePhaseCJobLock
소스: plan mode (C:\Users\rumdi\.claude\plans\hazy-booping-moore.md)

## 요구사항 요약

ai_jobs 테이블에 DB 기반 Job Lock을 추가하여 동일 branch에 대한 다중 agent 동시 실행을 방지한다.
PostgreSQL row-level atomic UPDATE로 "하나의 job에 하나의 agent만" 원칙을 강제하고,
/start(claim) → /pr(release) 사이 모든 단계에서 lock이 유지되도록 스킬 파일을 수정한다.

## 상세 요구사항

1. **Migration 004** — ai_jobs 테이블에 lock 관련 컬럼 6개 추가
   - `locked_by TEXT NULL` — lock 소유자 (git user.name 또는 agent 식별자)
   - `locked_at TIMESTAMPTZ NULL` — lock 획득 시각
   - `lock_expires_at TIMESTAMPTZ NULL` — lock 만료 시각 (기본 60분)
   - `lock_token TEXT NULL` — release/heartbeat 검증용 UUID
   - `agent_id TEXT NULL` — 실행 주체 (claude-code, n8n, auto-fix 등)
   - `last_heartbeat_at TIMESTAMPTZ NULL` — 마지막 생존 신호
   - 부분 인덱스: `lock_expires_at` (lock 있는 행만)

2. **`job_lock.py` 신규 작성** — CLI 서브커맨드 6개
   - `claim --branch BRANCH [--ttl 60] [--owner NAME] [--agent-id ID]`
     - atomic UPDATE: `WHERE lock_expires_at IS NULL OR lock_expires_at < NOW() OR locked_by = owner`
     - 성공 시 stdout에 `LOCK_TOKEN=<uuid>` 출력
     - 실패 시 현재 lock holder 정보(owner, expires_at) 출력 후 exit 1
   - `release --branch BRANCH --token TOKEN` — token 일치 확인 후 lock 해제
   - `heartbeat --branch BRANCH --token TOKEN [--ttl 60]` — lock_expires_at 연장
   - `status --branch BRANCH` — 현재 lock 상태 출력
   - `expire` — 만료된 lock 일괄 해제 (lock_expires_at < NOW())
   - `list-active` — 현재 lock 중인 job 목록 출력
   - exit code: 0=성공, 1=lock 실패, 2=job 없음, 3=DB 연결 실패

3. **token 저장** — `.ai_sdlc_lock` 파일 (프로젝트 루트, `.gitignore` 등록 필수)
   - 형식: `branch={브랜치명}\ntoken={lock_token}`

4. **`/start` SKILL.md 수정** — 1단계(task coding 전환) 직후, 2단계(명세 파일 탐색) 전
   - `job_lock.py claim` 실행; 실패 시 현재 lock 정보 출력 후 중단
   - 성공 시 `.ai_sdlc_lock` 파일 생성

5. **`/pr` SKILL.md 수정** — 5단계(완료 보고) 직전
   - `.ai_sdlc_lock`이 있으면 `job_lock.py release` 실행
   - release 실패는 WARN 처리 (PR 생성 차단 않음), 파일 삭제

6. **`/done` SKILL.md 수정** — 4.5단계(DB status=testing 갱신) 직후
   - `.ai_sdlc_lock`이 있으면 `job_lock.py heartbeat` 실행 (lock TTL 연장)

7. **`/workflow` SKILL.md 수정** — 각 단계(plan→req→impact→start→test-gen→done→review→pr) 사이
   - heartbeat 삽입, `--ttl 180` (workflow는 긴 작업)

8. **`session-start.sh` 수정** — `[SPRINT 현황]` 섹션 뒤
   - `job_lock.py list-active` 결과로 `[AI_SDLC Job Lock]` 섹션 추가
   - Active lock / Stale lock 구분 출력, lock 없으면 섹션 생략

9. **`check_sdlc_consistency.py` 수정** — 기존 9개 검사 항목 뒤에 추가
   - `stale_locks`: `lock_expires_at < NOW()` 인 행 → WARN
   - `invalid_locks`: `locked_by IS NOT NULL AND lock_token IS NULL` → CRITICAL (strict)

10. **`db_write.py` 수정** — `list-active` 출력에 lock 상태 컬럼 추가
    - `locked_by`, `lock_expires_at` 포함

11. **문서**
    - `Docs/operations/ai-sdlc-job-lock-policy.md` 신규: lock 정책 전문
    - `Docs/operations/ai-sdlc-auto-fix-policy.md` 업데이트: n8n lock 필수 정책 추가

## 영향 범위 (예상)

| 파일 | 유형 | 위험도 |
|------|------|--------|
| `.github/scripts/sdlc_db_migrations.sql` | SQL migration 추가 | LOW |
| `.github/scripts/job_lock.py` | 신규 Python 스크립트 | LOW |
| `.github/scripts/db_write.py` | list-active 출력 수정 | LOW |
| `.github/scripts/check_sdlc_consistency.py` | 검사 항목 추가 | LOW |
| `.claude/skills/start/SKILL.md` | lock claim 삽입 | MEDIUM (핵심 스킬) |
| `.claude/skills/pr/SKILL.md` | lock release 삽입 | MEDIUM (핵심 스킬) |
| `.claude/skills/done/SKILL.md` | heartbeat 삽입 | LOW |
| `.claude/skills/workflow/SKILL.md` | heartbeat 삽입 | LOW |
| `.claude/hooks/session-start.sh` | lock 섹션 추가 | LOW |
| `Docs/operations/*.md` | 문서 신규/수정 | LOW |
| `.gitignore` | 항목 추가 | LOW |

C# 코드 변경 없음 → 빌드/테스트 영향 없음.

## 제약 및 주의사항

- **ADR-009 준수**: PostgreSQL SDLC DB를 그대로 사용. 신규 외부 서비스 없음.
- **Phase C 원칙**: DB 연결 실패 시 graceful skip 없음 — exit 3로 중단.
- **멱등성**: claim은 `locked_by = owner` 조건으로 재진입 허용 (같은 owner 재실행 안전).
- **release 실패 비차단**: PR 생성보다 lock 해제가 덜 중요 — WARN 처리.
- **TTL 기본값**: 60분. /workflow는 180분. n8n auto-fix는 30분(정책 문서에 명시).
- **`.ai_sdlc_lock` 위치**: 프로젝트 루트 — gitignore 필수. Windows/Linux 경로 무관하게 `git rev-parse --show-toplevel` 기준.

## 구현 접근 방향

1. Migration 004 SQL 블록 추가 → docker exec로 직접 적용 (PostgreSQL 컨테이너)
2. `job_lock.py` 작성 시 `db_write.py`의 DB 연결 패턴(`_get_conn()`) 재사용
3. 스킬 파일 수정은 기존 DB 호출 블록 직후에 삽입 — 구조 변경 최소화
4. `session-start.sh`는 `job_lock.py list-active --format=session` 서브커맨드를 별도 추가하여 파싱 부담 최소화

## 검증 기준

```bash
# 1. Migration 적용 후 컬럼 확인
docker exec sdlc-postgres psql -U sdlc_user -d sdlc_db -c "\d sdlc.ai_jobs" | grep locked

# 2. claim/release/expire 단위 테스트
python .github/scripts/job_lock.py claim --branch test-lock-branch        # → LOCK_TOKEN=...
python .github/scripts/job_lock.py claim --branch test-lock-branch        # → exit 1 (중복 차단)
python .github/scripts/job_lock.py release --branch test-lock-branch --token <token>  # → 성공
python .github/scripts/job_lock.py claim --branch test-lock-branch        # → 재획득 성공

# 3. 빌드/테스트
cd PlatformA && dotnet build PlatformA.sln && dotnet test PlatformA.sln

# 4. consistency check
python .github/scripts/check_sdlc_consistency.py --check --strict

# 5. session-start 출력 (lock 없을 때 섹션 미출력 확인)
bash .claude/hooks/session-start.sh
```

## DESIGN_REVIEW 결과

| ADR | 관련 여부 | 충돌/참고 사항 |
|-----|---------|--------------|
| ADR-009: PostgreSQL SDLC DB | 관련 있음 | 기존 ai_jobs 테이블 확장 — 신규 서비스 없음, ADR 범위 내 |
| ADR-008: n8n Event Orchestrator | 관련 있음 (참고) | n8n이 lock을 획득해야 한다는 정책 추가 — n8n 자체 변경 없음 |
| ADR-001~007 | 없음 | C# 코드 미변경, 패킷/API/Redis 패턴 무관 |

판정: ✅ 기존 ADR 준수 — 기존 PostgreSQL 인프라 내 테이블 확장, 신규 외부 서비스 없음.
