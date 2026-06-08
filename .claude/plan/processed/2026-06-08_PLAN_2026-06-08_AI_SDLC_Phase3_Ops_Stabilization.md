# PLAN — 2026-06-08 AI_SDLC Phase 3 운영 안정화

작성일: 2026-06-08  
대상 프로젝트: PlatformA  
작업 브랜치 권장명: `2026-06-08_StabilizeSdlcPhase3Ops`  
권장 스프린트: `#49`  
작업 목적: 2026-06-05에 급격히 확장된 AI_SDLC Phase 3를 안정화한다. append-only 파일 충돌, DB/JSON 정합성, DB write 실패 가시성, 자동 수정 safety policy를 정리하여 “믿고 반복 실행할 수 있는 AI_SDLC”로 만든다.

---

## 0. 배경

2026-06-05 작업에서 AI_SDLC Phase 3가 크게 확장되었다.

완료된 주요 작업:

```text
PR #72 — Phase 3 데이터 흐름 안정화
- ai_failures 중복 삽입 방지
- task JSON → PostgreSQL 이전
- migrate_tasks_to_postgres.py --apply 구현

PR #73 — /workflow 오케스트레이터
- plan → requirement → impact → start → coding → test-gen → done → review → pr 자동 체이닝

PR #74 — Phase3 자동화 완성
- repository_dispatch → n8n → /qa-failure 자동 실행 기반
- auto-fix.yml
- plan-file-trigger.yml

PR #75 — ai_model_runs 연동
- /pr 완료 시 PostgreSQL sdlc.ai_model_runs 기록

PR #76 — LLM 라우터
- impact.risk 기반 Haiku/Sonnet/Opus 자동 선택

PR #77 — PostgreSQL primary 전환
- db_write.py 추가
- 7개 스킬 dual-write
- /pr 게이트 DB 우선 조회 + 파일 fallback
```

현재 AI_SDLC는 다음 단계까지 왔다.

```text
파일 기반 SDLC
→ PostgreSQL primary 지향
→ n8n 이벤트 감지
→ 자동 수정 루프
→ ai_model_runs 비용 기록
→ LLM 라우터
```

하지만 2026-06-05 workreport에서 중요한 운영 이슈가 확인되었다.

```text
SPRINT.md / cost-log.md append-only 파일 충돌 패턴
동일 날짜 다수 브랜치 작업 시 merge conflict 반복 발생
```

또한 Phase 3가 빠르게 확장되었으므로 다음 안정화가 필요하다.

```text
1. append-only 파일 충돌 완화
2. DB/JSON 정합성 검사
3. DB write 실패 가시화
4. 자동 수정 safety policy 문서화
5. PostgreSQL primary 전환 이후 fallback 기준 명확화
```

---

## 1. 오늘 작업의 목표

2026-06-08 작업의 목표는 새 기능 추가가 아니다.

핵심 목표:

```text
Phase 3 운영 안정화
```

구체적으로는 다음 상태를 만든다.

```text
Before:
- PostgreSQL primary 전환 시작
- task JSON fallback 유지
- /workflow, auto-fix, LLM router 도입
- SPRINT.md / cost-log.md 충돌 가능성 존재

After:
- append-only 충돌을 줄이는 파일 구조 도입
- DB와 JSON 상태 불일치를 감지하는 스크립트 도입
- DB write 실패가 로그와 session-start에 표시됨
- 자동 수정 허용 범위가 문서화됨
- Phase 3를 더 안전하게 운영할 수 있음
```

---

## 2. 이번 작업 범위

### 포함

```text
1. SPRINT.md append-only 충돌 완화 구조 설계
2. AI/sprints/ 스프린트별 개별 파일 구조 도입
3. SPRINT.md를 인덱스/요약 파일로 축소
4. cost-log.md append-only 문제 개선 방향 문서화
5. DB/JSON 정합성 검사 스크립트 작성
6. db_write.py 실패 로그 기록 추가
7. session-start.sh에서 최근 DB write 실패 표시
8. 자동 수정 safety policy 문서 작성
9. Sprint #49 등록
10. 2026-06-08 workreport 작성
```

### 제외

```text
- K8s 도입
- 제품 기능 개발
- 자동 수정 범위 확대
- 새 n8n workflow 대량 추가
- DB primary 강제 모드 전환
- task JSON 제거
- cost-log.md 완전 제거
- SPRINT.md 완전 제거
```

---

## 3. 작업 원칙

```text
1. 오늘은 안정화 작업이다.
2. 기능 확장보다 반복 실행 안정성을 우선한다.
3. 기존 파일 기반 fallback은 유지한다.
4. append-only 파일의 충돌 가능성을 줄인다.
5. DB write 실패는 SDLC를 차단하지 않되, 반드시 눈에 보이게 한다.
6. 자동 수정은 허용 범위를 명확히 문서화한다.
7. Phase 3 운영 안정성 확보 전까지 task JSON 제거는 하지 않는다.
```

---

# TASK 1. Sprint #49 등록

## 목적

6월 8일 안정화 작업을 AI_SDLC 스프린트에 등록한다.

## 수정 파일

```text
AI/SPRINT.md
```

## 추가 내용 예시

```markdown
---

## 스프린트 #49 (2026-06-08 ~)
**목표**: AI_SDLC Phase 3 운영 안정화 — append-only 충돌 완화, DB/JSON 정합성 검사, DB write 실패 가시화, 자동 수정 safety policy 정리

### 진행 중

- [ ] `AI/sprints/` 구조 도입 — 스프린트별 개별 파일 관리
- [ ] `AI/SPRINT.md` 인덱스/요약 역할로 정리
- [ ] `check_sdlc_consistency.py` — PostgreSQL ↔ task JSON 정합성 검사
- [ ] `db_write.py` 실패 로그 기록 추가
- [ ] `session-start.sh` 최근 DB write 실패 표시
- [ ] `Docs/operations/ai-sdlc-auto-fix-policy.md` 작성
- [ ] `Docs/operations/ai-sdlc-append-only-conflict-policy.md` 작성
- [ ] 2026-06-08 workreport 작성
```

## 완료 기준

```text
- SPRINT.md에 Sprint #49가 등록됨
- 가능하면 신규 스프린트 상세는 AI/sprints/sprint-049.md에 작성됨
- SPRINT.md는 새 구조 전환 방향을 설명함
```

---

# TASK 2. `AI/sprints/` 구조 도입

## 목적

`SPRINT.md`가 모든 스프린트를 계속 append하는 구조에서 벗어나도록 한다. 동일 날짜 다중 브랜치 작업 시 merge conflict를 줄인다.

## 신규 디렉터리

```text
AI/sprints/
```

## 신규 파일

```text
AI/sprints/README.md
AI/sprints/sprint-049.md
```

## `AI/sprints/README.md` 내용

```markdown
# AI_SDLC Sprints

이 디렉터리는 AI_SDLC 스프린트별 상세 작업 파일을 보관한다.

## 목적

기존 `AI/SPRINT.md`는 append-only 구조라서 같은 날짜에 여러 브랜치가 생성될 경우 merge conflict가 자주 발생한다.

이를 완화하기 위해 스프린트별 상세 내용은 개별 파일로 분리한다.

## 구조

- `AI/SPRINT.md`: 스프린트 인덱스/요약
- `AI/sprints/sprint-NNN.md`: 개별 스프린트 상세
```

## `AI/sprints/sprint-049.md` 예시

```markdown
# Sprint #49 — AI_SDLC Phase 3 운영 안정화

작성일: 2026-06-08  
브랜치: `2026-06-08_StabilizeSdlcPhase3Ops`  
규모: M 또는 L  
위험도: MEDIUM 또는 HIGH

## 목표

AI_SDLC Phase 3 운영 안정화:
- append-only 파일 충돌 완화
- PostgreSQL ↔ task JSON 정합성 검사
- DB write 실패 가시화
- 자동 수정 safety policy 정리

## 작업 목록

- [ ] `AI/sprints/` 구조 도입
- [ ] `AI/SPRINT.md` 인덱스화
- [ ] `check_sdlc_consistency.py` 작성
- [ ] `db_write.py` 실패 로그 기록
- [ ] `session-start.sh` DB write 실패 표시
- [ ] `Docs/operations/ai-sdlc-auto-fix-policy.md` 작성
- [ ] `Docs/operations/ai-sdlc-append-only-conflict-policy.md` 작성
```

## 완료 기준

```text
- AI/sprints/README.md 생성
- AI/sprints/sprint-049.md 생성
- SPRINT.md에는 sprint-049.md 링크 또는 경로가 기록됨
```

---

# TASK 3. `AI/SPRINT.md` 인덱스화

## 목적

`AI/SPRINT.md`를 모든 스프린트 상세를 직접 append하는 파일에서, 스프린트 목록과 현재 진행 상태를 보여주는 인덱스로 점진 전환한다.

## 수정 파일

```text
AI/SPRINT.md
```

## 권장 구조

```markdown
# AI_SDLC Sprint Index

이 파일은 스프린트 인덱스와 현재 진행 상태만 관리한다.  
스프린트별 상세 작업은 `AI/sprints/sprint-NNN.md` 파일을 사용한다.

## Active Sprint

| Sprint | Title | Status | File |
|---|---|---|---|
| #49 | AI_SDLC Phase 3 운영 안정화 | 진행 중 | `AI/sprints/sprint-049.md` |

## Recent Sprints

| Sprint | Title | Status | File |
|---|---|---|---|
| #48 | PostgreSQL primary 전환 | 완료 | 기존 task 참조 |
| #47 | LLM 라우터 | 완료 | 기존 task 참조 |
| #46 | ai_model_runs 연동 | 완료 | 기존 task 참조 |
```

## 주의

한 번에 기존 SPRINT.md 전체를 대규모 재구성하지 않아도 된다.  
오늘은 새 스프린트부터 `AI/sprints/` 구조를 사용하도록 시작하면 충분하다.

## 완료 기준

```text
- SPRINT.md에 새 구조 안내 추가
- Sprint #49가 별도 파일로 연결됨
- 기존 스프린트 내역은 보존
```

---

# TASK 4. cost-log append-only 충돌 개선 방향 문서화

## 목적

`AI/cost-log.md`의 append-only 충돌 문제를 해결하기 위한 방향을 정리한다.

6월 5일 이후 `sdlc.ai_model_runs`가 비용/토큰 데이터의 DB 저장소가 되었다. 따라서 장기적으로 `AI/cost-log.md`는 primary가 아니라 report/export 파일이 되어야 한다.

## 신규 문서

```text
Docs/operations/ai-sdlc-append-only-conflict-policy.md
```

## 문서에 포함할 내용

```markdown
# AI_SDLC Append-only 파일 충돌 완화 정책

## 문제

`AI/SPRINT.md`, `AI/cost-log.md`는 여러 브랜치가 같은 날짜에 동시에 수정할 경우 merge conflict가 발생하기 쉽다.

## 정책

### SPRINT.md

- `AI/SPRINT.md`는 인덱스/요약 파일로 축소한다.
- 스프린트 상세는 `AI/sprints/sprint-NNN.md`에 작성한다.

### cost-log.md

- 장기적으로 `sdlc.ai_model_runs`를 primary로 사용한다.
- `AI/cost-log.md`는 DB에서 생성되는 report/export 파일로 전환한다.
- 여러 브랜치가 직접 append하지 않도록 한다.

### workreport

- `AI/workreport/YYYY-MM-DD.md`는 날짜별 파일이므로 비교적 충돌 위험이 낮다.
- 단, 같은 날짜에 여러 PR이 동시에 workreport를 수정하면 충돌 가능성이 있으므로 마지막에 한 번 정리하는 방식을 권장한다.
```

## 완료 기준

```text
- append-only 충돌 문제와 해결 방향이 문서화됨
- SPRINT.md와 cost-log.md의 역할 변경 방향이 명확함
```

---

# TASK 5. DB/JSON 정합성 검사 스크립트 작성

## 목적

PostgreSQL primary 전환 이후에도 `AI/tasks/*.json` fallback이 남아 있으므로, DB와 파일 간 상태 불일치를 감지한다.

## 신규 파일

```text
.github/scripts/check_sdlc_consistency.py
```

## 기능

```text
1. AI/tasks/*.json 읽기
2. PostgreSQL sdlc.ai_jobs 조회
3. PostgreSQL sdlc.ai_job_steps 조회
4. branch 기준으로 JSON ↔ DB 매칭
5. 누락/불일치 검사
6. 요약 리포트 출력
7. exit code 정책 제공
```

## 검사 항목

```text
- DB에는 있는데 task JSON 파일이 없음
- task JSON에는 있는데 DB에는 없음
- status 불일치
- test_generated 불일치
- review_completed 불일치
- pr_url 불일치
- impact.risk 불일치
- steps 수 불일치
- ai_model_runs 누락
```

## CLI

```bash
python .github/scripts/check_sdlc_consistency.py --check
python .github/scripts/check_sdlc_consistency.py --check --strict
```

## 동작 정책

```text
기본 모드:
- 불일치가 있어도 exit 0
- warning 출력

strict 모드:
- critical mismatch가 있으면 exit 1
```

## DB 연결 실패 시

```text
- stderr에 경고 출력
- exit 0
- "DB unavailable; consistency check skipped" 표시
```

초기에는 SDLC를 막지 않는 것이 우선이다.

## 출력 예시

```text
AI_SDLC Consistency Check

Task JSON files: 45
DB jobs: 45
DB steps: 220
DB model runs: 40

Missing in DB: 0
Missing in files: 0
Status mismatches: 0
Gate mismatches: 0
Step count mismatches: 2
Model run missing: 3

Result: WARN
```

## 완료 기준

```text
- DB와 JSON 비교 가능
- DB 미실행 시 graceful skip
- 불일치 요약 출력
- strict 모드 지원
```

---

# TASK 6. `db_write.py` 실패 로그 기록 추가

## 목적

현재 `db_write.py || true` 구조는 SDLC를 막지 않는 장점이 있지만, DB 기록 실패가 숨겨질 수 있다. 실패를 별도 로그로 남겨 추후 확인 가능하게 한다.

## 수정 파일

```text
.github/scripts/db_write.py
```

## 신규 디렉터리

```text
AI/logs/db-write-failures/
```

## 동작

DB write 실패 시:

```text
1. stderr에 경고 출력
2. AI/logs/db-write-failures/YYYY-MM-DD.log 파일에 append
3. 실패 action, branch, message, timestamp 기록
4. exit 0 유지
```

## 로그 예시

```text
[2026-06-08T10:30:12+09:00] action=upsert-job branch=2026-06-08_StabilizeSdlcPhase3Ops error=connection refused
[2026-06-08T10:35:42+09:00] action=insert-step branch=2026-06-08_StabilizeSdlcPhase3Ops step=impact error=timeout
```

## 주의

- DB write 실패가 있어도 SDLC 흐름을 막지 않는다.
- 실제 DB 비밀번호나 connection string 전체를 로그에 남기지 않는다.
- 로그 파일이 너무 커지지 않도록 날짜별 파일로 분리한다.

## 완료 기준

```text
- DB 연결 실패 시 로그 파일 생성
- 민감 정보가 로그에 남지 않음
- 기존 `|| true` 사용 방식과 호환
```

---

# TASK 7. `session-start.sh`에서 최근 DB write 실패 표시

## 목적

개발 세션 시작 시 최근 DB write 실패를 보여줘서, DB 기록 누락을 조기에 인지한다.

## 수정 파일

```text
.claude/hooks/session-start.sh
```

> **주의**: `.claude/session-start.sh`가 아니다. 실제 파일은 `.claude/hooks/session-start.sh`이다.

## TASK 2/3과의 조율 (중요)

TASK 2/3에서 SPRINT.md가 테이블 인덱스로 바뀌면 `session-start.sh`의 아래 grep이 빈 결과를 반환한다:

```bash
# 현재 코드 (SPRINT.md가 인덱스가 되면 동작 안 함)
IN_PROGRESS=$(grep -A 20 "## 진행 중" "$SPRINT_FILE" | grep -E "^\- \[ \]" | head -10)
COMPLETED=$(grep -E "^\- \[x\]" "$SPRINT_FILE" | tail -5)
```

따라서 session-start.sh를 아래 순서로 업데이트해야 한다:

1. `AI/SPRINT.md`에서 Active Sprint 파일 경로를 읽는다 (`AI/sprints/sprint-NNN.md`)
2. 해당 파일에서 `- [ ]` 와 `- [x]` 항목을 읽는다
3. 파일이 없거나 기존 형식이면 기존 grep 방식으로 fallback한다
4. DB write 실패 표시는 기존 로직 뒤에 추가한다

## 표시 기준

```text
최근 24시간 이내 AI/logs/db-write-failures/*.log가 있으면 표시
```

## 출력 예시

```text
⚠️  Recent AI_SDLC DB write failures detected

AI/logs/db-write-failures/2026-06-08.log
- upsert-job failed: connection refused
- insert-step failed: timeout

Run:
  cat AI/logs/db-write-failures/2026-06-08.log
```

## 완료 기준

```text
- 최근 DB write 실패가 있으면 session-start에서 표시
- 실패 로그가 없으면 조용히 통과
- 세션 시작이 실패하지 않음
```

---

# TASK 8. 자동 수정 safety policy 문서 작성

## 목적

Phase 3 자동 수정 루프가 도입되었으므로, 어떤 실패 유형을 자동 수정할 수 있고 어떤 변경은 사람 승인 또는 금지가 필요한지 명확히 문서화한다.

## 신규 문서

```text
Docs/operations/ai-sdlc-auto-fix-policy.md
```

## 문서 구조

```markdown
# AI_SDLC Auto-fix Safety Policy

## 1. 목적

AI_SDLC 자동 수정 루프의 허용 범위와 금지 범위를 정의한다.

## 2. 기본 원칙

- 자동 수정은 기본적으로 LOW risk 작업에 한정한다.
- HIGH risk 작업은 자동 수정하지 않는다.
- DB schema, auth, security, payment, deployment 변경은 사람 승인 필수다.
- 자동 수정은 반드시 diff와 요약을 남긴다.
- 자동 수정 retry_count는 제한한다.

## 3. failure_type별 정책

| failure_type | 자동 분석 | 자동 수정 | 사람 승인 | 비고 |
|---|---:|---:|---:|---|
| format_failed | Yes | Yes | No | dotnet format, whitespace |
| style_failed | Yes | Yes | No | 문서/스타일 |
| docs_failed | Yes | Yes | Optional | DocFX, markdown |
| build_failed | Yes | Limited | Yes | LOW/MEDIUM만 제한적 |
| test_failed | Yes | No | Yes | 테스트 실패 자동 수정은 위험 |
| sdlc_gate_failed | Yes | Limited | Yes | 누락 단계 보정 가능 |
| db_migration_failed | Yes | No | Yes | DB schema 변경 위험 |
| security_failed | Yes | No | Yes | 자동 수정 금지 |
| deploy_failed | Yes | No | Yes | 환경 의존성 큼 |

## 4. risk별 정책

| risk | 자동 수정 |
|---|---|
| LOW | 허용 |
| MEDIUM | 제한적 허용 |
| HIGH | 금지 또는 명시 승인 필요 |
```

## 완료 기준

```text
- 자동 수정 허용/금지 범위가 명확함
- failure_type/risk별 정책이 표로 정리됨
- 향후 auto-fix.yml과 /qa-failure에서 참고 가능
```

---

# TASK 8.5. `/plan` 스킬 스프린트 카운터 수정

## 목적

TASK 2/3에서 `AI/SPRINT.md`가 인덱스 테이블로 바뀌면 `/plan` 스킬의 스프린트 번호 자동 계산이 깨진다.

현재 `/plan` 스킬의 sprint 카운터 코드:
```bash
EXISTING=$(grep -c "^## 스프린트 #" AI/SPRINT.md 2>/dev/null || echo "0")
SPRINT_NUM=$((EXISTING + 1))
```

SPRINT.md가 테이블 구조가 되면 `grep "^## 스프린트 #"`가 0을 반환하여 항상 Sprint #1이 할당된다.

## 수정 파일

```text
.claude/skills/plan/SKILL.md
```

## 수정 방향

`SPRINT_NUM` 계산을 `AI/tasks/sprint*.json` 파일 수 기반으로 변경한다:

```bash
# 기존 방식 (SPRINT.md 의존 — SPRINT.md 구조 변경 시 깨짐)
EXISTING=$(grep -c "^## 스프린트 #" AI/SPRINT.md 2>/dev/null || echo "0")

# 신규 방식 (task JSON 파일 수 기반 — SPRINT.md 구조 무관)
EXISTING=$(ls AI/tasks/sprint*.json 2>/dev/null | wc -l | tr -d ' ')
SPRINT_NUM=$((EXISTING + 1))
```

task JSON 파일은 sprint 번호를 파일명에 포함(`sprint46_*.json`)하므로, 파일 수 = 최대 sprint 번호가 된다.

## 완료 기준

```text
- /plan 스킬이 AI/tasks/sprint*.json 파일 수 기반으로 SPRINT_NUM을 계산
- SPRINT.md 구조가 바뀌어도 번호가 올바르게 증가
```

---

# TASK 8.7. dual-write → DB-only 전환 기준 문서

## 목적

"당분간은 파일과 DB 체계 2개를 병행하면서 나중에는 완전히 DB 체계로 이관한다"는 정책을 구체화한다. 전환 시점의 기준이 없으면 dual-write가 무기한 지속될 수 있다.

## 신규 문서

```text
Docs/operations/ai-sdlc-db-migration-roadmap.md
```

## 문서에 포함할 내용

```markdown
# AI_SDLC DB Primary 전환 로드맵

## 현재 상태 (2026-06-08 기준)

- 파일 (task JSON, SPRINT.md, cost-log.md): primary/fallback
- PostgreSQL (sdlc.ai_jobs, ai_job_steps, ai_model_runs): secondary

## 장기 목표

파일 기반 SDLC를 완전히 DB primary로 전환한다.

## 단계별 전환 계획

### Phase A — 현재: dual-write (파일 우선)
- 모든 SDLC 스킬이 파일 + DB 동시 기록
- DB 실패 시 파일 fallback
- `check_sdlc_consistency.py`로 불일치 감지

### Phase B — 전환 조건 충족 후: DB 우선
- DB write 실패율 7일 평균 < 1%
- `check_sdlc_consistency.py` strict 모드에서 0 불일치
- 모든 스킬의 gate 검사가 DB SELECT로 작동 확인
- PostgreSQL 고가용성 구성 또는 정기 백업 검증

### Phase C — DB 단독 (task JSON 제거)
- Phase B가 30일 이상 안정화 후
- task JSON 파일 역할을 DB migration history로 대체
- SPRINT.md, cost-log.md를 DB 생성 report로 완전 전환

## 전환 금지 조건

- DB 연결 실패가 하루에 1회 이상 발생하는 경우
- check_sdlc_consistency.py strict 모드에서 불일치 > 0인 경우
- PostgreSQL 인스턴스에 백업 정책이 없는 경우
```

## 완료 기준

```text
- dual-write → DB-only 전환 기준이 문서화됨
- 현재 Phase A 상태가 명시됨
- Phase B 전환 조건이 구체적으로 정의됨
```

---

# TASK 9. 테스트 및 검증

## 9.1 Python script 검증

```bash
python .github/scripts/check_sdlc_consistency.py --check
python .github/scripts/check_sdlc_consistency.py --check --strict
```

DB가 꺼진 상태에서도 graceful skip이 되는지 확인한다.

## 9.2 DB write 실패 로그 검증

의도적으로 잘못된 connection string을 넣어 확인한다.

```bash
SDLC_DB_CONNECTION="Host=localhost;Port=9999;Database=bad;Username=bad;Password=bad" \
  python .github/scripts/db_write.py --action upsert-job --branch test --status testing
```

> **주의**: `db_write.py`의 CLI는 positional이 아니라 `--action` 플래그 방식이다.

기대:

```text
- exit 0
- stderr warning
- AI/logs/db-write-failures/YYYY-MM-DD.log 생성
```

## 9.3 session-start 검증

```bash
bash .claude/hooks/session-start.sh
```

기대:

```text
- 최근 DB write failure 로그가 있으면 표시
- 없으면 표시하지 않음
```

## 9.4 .NET build/test

```bash
cd PlatformA
dotnet build PlatformA.sln
dotnet test PlatformA.sln
```

## 완료 기준

```text
- check_sdlc_consistency.py 동작
- db_write.py 실패 로그 기록
- session-start 표시 동작
- dotnet build/test 통과
```

---

# TASK 10. workreport 작성

## 신규 파일

```text
AI/workreport/2026-06-08.md
```

## 포함 내용

```text
1. 오늘 작업 요약
2. append-only 충돌 완화 내용
3. AI/sprints/ 구조 도입 여부
4. DB/JSON 정합성 검사 결과
5. db_write 실패 로그 가시화 결과
6. 자동 수정 safety policy 요약
7. 검증 결과
8. 남은 이슈
9. 다음 작업
```

---

# 예상 변경 파일

```text
AI/SPRINT.md
AI/sprints/README.md
AI/sprints/sprint-049.md
AI/workreport/2026-06-08.md
AI/logs/db-write-failures/.gitkeep

.github/scripts/check_sdlc_consistency.py
.github/scripts/db_write.py

.claude/hooks/session-start.sh          ← 실제 경로 (session-start.sh 아님)
.claude/skills/plan/SKILL.md            ← sprint 카운터 수정 (TASK 8.5)

Docs/operations/ai-sdlc-auto-fix-policy.md
Docs/operations/ai-sdlc-append-only-conflict-policy.md
Docs/operations/ai-sdlc-db-migration-roadmap.md  ← 신규 (TASK 8.7)
```

---

# 권장 PR 제목

```text
chore: AI_SDLC Phase 3 운영 안정화 — append-only 충돌 완화 및 DB 정합성 검사
```

또는:

```text
chore: stabilize AI_SDLC Phase 3 ops
```

---

# 위험도 평가

권장 risk:

```text
MEDIUM~HIGH
```

이유:

```text
- SDLC 핵심 운영 파일 구조 변경
- db_write.py 동작 변경
- session-start 변경
- 자동 수정 정책 문서화
- PostgreSQL primary 전환 이후 정합성 검사 추가
```

코드 서비스 런타임 위험도는 낮지만, AI_SDLC 공정 자체에 영향이 있으므로 review는 필수다.

---

# 완료 기준 요약

오늘 작업 완료 조건:

```text
1. AI/sprints/ 구조 도입
2. SPRINT.md 인덱스화 시작
3. append-only 충돌 완화 정책 문서 작성
4. check_sdlc_consistency.py 작성
5. DB/JSON 정합성 검사 가능
6. db_write.py 실패 로그 기록
7. .claude/hooks/session-start.sh 최근 DB write 실패 표시 + AI/sprints/ 파일 읽기
8. 자동 수정 safety policy 문서 작성
8.5. /plan 스킬 sprint 카운터 수정 (task JSON 파일 수 기반)
8.7. dual-write → DB-only 전환 기준 문서 작성
9. dotnet build/test 통과
10. 2026-06-08 workreport 작성
```

---

## 결론

6월 8일 작업의 핵심은 Phase 3를 더 화려하게 만드는 것이 아니다.

핵심은 다음이다.

```text
빠르게 확장된 Phase 3를
충돌 없이,
정합성 있게,
실패가 보이게,
안전한 자동화 정책 아래에서
운영 가능한 상태로 안정화한다.
```
