# AI_SDLC Job Lock 정책

## 목적

동일 branch/job에 대한 다중 agent 동시 실행을 방지한다.
"하나의 job에 하나의 agent만" 원칙을 PostgreSQL row-level atomic UPDATE로 강제한다.

## 배경

Phase C(DB 단독 운영) 이후 파일 충돌은 해소됐지만, 두 agent가 동시에 같은 job을
처리하면 DB 상태 역행과 step 기록 충돌이 발생할 수 있다.

## 구조

### DB 컬럼 (ai_jobs 테이블)

| 컬럼 | 설명 |
|------|------|
| `locked_by` | lock 소유자 (git user.name 또는 agent 식별자). NULL이면 미잠금. |
| `locked_at` | lock 획득 시각 |
| `lock_expires_at` | lock 만료 시각. 기본 TTL: 60분 |
| `lock_token` | release/heartbeat 검증용 UUID |
| `agent_id` | 실행 주체 (claude-code, n8n, auto-fix 등) |
| `last_heartbeat_at` | 마지막 heartbeat 시각 |

### 로컬 파일

`.ai_sdlc_lock` (프로젝트 루트, gitignore 등록):
```
branch=2026-06-11_ExampleBranch
token=<uuid>
```

## 워크플로

```
/start (1.5단계)
  └─ job_lock.py claim --branch BRANCH
        ├─ 성공: .ai_sdlc_lock 생성, 작업 계속
        └─ 실패: 현재 lock holder 출력 후 즉시 중단

/done (4.5단계 직후)
  └─ job_lock.py heartbeat --branch BRANCH --token TOKEN

/workflow (6.5단계)
  └─ job_lock.py heartbeat --branch BRANCH --token TOKEN --ttl 180

/pr (4.8단계)
  └─ job_lock.py release --branch BRANCH --token TOKEN
        └─ 실패 시 WARN (PR 생성 차단 않음)
```

## claim 정책

atomic UPDATE 조건:
```sql
WHERE lock_expires_at IS NULL      -- 아무도 점유 중 아님
   OR lock_expires_at < NOW()      -- TTL 만료됨
   OR locked_by = %(owner)s        -- 내가 이미 점유 중 (재진입 허용)
```

UPDATE 0행 → lock 획득 실패 → exit 1

## TTL 정책

| 상황 | TTL |
|------|-----|
| 일반 작업 (`/start`) | 60분 |
| 긴 작업 (`/workflow`) | 180분 |
| n8n auto-fix | 30분 |

## heartbeat

- `/done` 4.5단계 이후에 호출 (빌드/테스트 완료 후 TTL 연장)
- `/workflow` 6.5단계에서 `/done` 실행 전에 호출

heartbeat 실패 시: WARN (작업 차단 않음). token 불일치는 이미 다른 agent가 lock을 재획득한 신호.

## stale lock 처리

TTL 만료된 lock은:
1. 자동: 다른 agent의 claim 시 atomic UPDATE 조건에 의해 덮어씌워짐
2. 수동: `python .github/scripts/job_lock.py expire`
3. 감지: `session-start.sh`의 `[AI_SDLC Job Lock]` 섹션에 표시
4. 감지: `check_sdlc_consistency.py`의 `stale_locks` 항목

## rollback (lock 제거)

lock 기능을 비활성화하려면:
1. `/start` SKILL.md의 1.5단계 제거
2. `/pr` SKILL.md의 4.8단계 제거
3. `/done`, `/workflow`의 heartbeat 제거
4. `ai_jobs` 테이블 lock 컬럼은 그대로 유지 가능 (데이터 영향 없음)

## n8n auto-fix 정책

n8n auto-fix가 동일 job에서 실행될 때:
- `job_lock.py claim --branch BRANCH --agent-id n8n --ttl 30` 획득 필수
- lock 획득 실패 시: PR comment만 남기고 작업 중단
- 획득 성공 시: 최대 30분 내 완료 후 release

자세한 n8n 정책: [ai-sdlc-auto-fix-policy.md](ai-sdlc-auto-fix-policy.md)
