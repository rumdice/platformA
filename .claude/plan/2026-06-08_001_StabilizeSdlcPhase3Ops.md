# 요구사항 명세: StabilizeSdlcPhase3Ops

작성일: 2026-06-08
브랜치: 2026-06-08_StabilizeSdlcPhase3Ops
소스: .claude/plan/PLAN_2026-06-08_AI_SDLC_Phase3_Ops_Stabilization.md

## 요구사항 요약

2026-06-05에 급격히 확장된 AI_SDLC Phase 3(PostgreSQL dual-write, LLM 라우터, /workflow 오케스트레이터)를
운영 안정화한다. append-only 파일 충돌 완화, DB/JSON 정합성 검사, DB write 실패 가시화,
자동 수정 safety policy 문서화, dual-write→DB-only 전환 기준 정의를 통해
"믿고 반복 실행할 수 있는 AI_SDLC"를 만든다.

## 상세 요구사항

### 1. AI/sprints/ 구조 도입 (TASK 1~3)
- `AI/sprints/` 디렉터리 생성
- `AI/sprints/README.md` — 구조 설명
- `AI/sprints/sprint-049.md` — Sprint #49 상세
- `AI/SPRINT.md`를 인덱스/요약 파일로 점진 전환 (기존 내역 보존, 새 스프린트부터 AI/sprints/ 사용)
- 목적: 동일 날짜 다수 브랜치 작업 시 SPRINT.md append-only merge conflict 감소

### 2. cost-log 충돌 완화 정책 문서 (TASK 4)
- `Docs/operations/ai-sdlc-append-only-conflict-policy.md` 신규 작성
- SPRINT.md → AI/sprints/, cost-log.md → DB report 전환 방향 문서화

### 3. DB/JSON 정합성 검사 스크립트 (TASK 5)
- `.github/scripts/check_sdlc_consistency.py` 신규 작성
- AI/tasks/*.json ↔ PostgreSQL sdlc.ai_jobs/ai_job_steps 비교
- 기본 모드: exit 0 + warning 출력
- strict 모드: critical mismatch 시 exit 1
- DB 연결 실패 시 graceful skip (exit 0)

### 4. db_write.py 실패 로그 기록 (TASK 6)
- `.github/scripts/db_write.py` 수정
- DB write 실패 시 `AI/logs/db-write-failures/YYYY-MM-DD.log`에 append
- 로그 포맷: `[timestamp] action=... branch=... error=...`
- 민감 정보(password, connection string) 로그 제외
- exit 0 유지

### 5. session-start.sh 업데이트 (TASK 7)
- `.claude/hooks/session-start.sh` 수정
- `AI/SPRINT.md`에서 Active Sprint 파일 경로 추출 → `AI/sprints/sprint-NNN.md` 읽기
- 기존 grep 방식 fallback 유지
- 최근 24시간 이내 `AI/logs/db-write-failures/*.log`가 있으면 표시

### 6. 자동 수정 safety policy 문서 (TASK 8)
- `Docs/operations/ai-sdlc-auto-fix-policy.md` 신규 작성
- failure_type/risk별 자동 수정 허용/금지 정책 정의
- PR 머지는 항상 사람이 직접 수행 (영구 정책 명시)

### 7. /plan 스킬 sprint 카운터 수정 (TASK 8.5)
- `.claude/skills/plan/SKILL.md` 수정
- `grep -c "^## 스프린트 #" AI/SPRINT.md` → `ls AI/tasks/sprint*.json | wc -l` 방식으로 변경
- SPRINT.md 구조 변경에 무관하게 sprint 번호 올바르게 증가

### 8. dual-write→DB-only 전환 기준 문서 (TASK 8.7)
- `Docs/operations/ai-sdlc-db-migration-roadmap.md` 신규 작성
- Phase A(현재: dual-write), Phase B(DB 우선), Phase C(DB 단독) 단계별 전환 계획
- Phase B 전환 조건 구체적으로 정의 (DB write 실패율, consistency check, 백업 정책)

## 영향 범위 (예상)

| 파일 | 변경 유형 | 비고 |
|------|---------|------|
| `AI/SPRINT.md` | 수정 | 인덱스 구조 추가 |
| `AI/sprints/README.md` | 신규 | 구조 설명 |
| `AI/sprints/sprint-049.md` | 신규 | Sprint #49 상세 |
| `AI/logs/db-write-failures/.gitkeep` | 신규 | 로그 디렉터리 |
| `.github/scripts/check_sdlc_consistency.py` | 신규 | DB/JSON 정합성 검사 |
| `.github/scripts/db_write.py` | 수정 | 실패 로그 추가 |
| `.claude/hooks/session-start.sh` | 수정 | DB 실패 표시 + sprints 읽기 |
| `.claude/skills/plan/SKILL.md` | 수정 | sprint 카운터 방식 변경 |
| `Docs/operations/ai-sdlc-auto-fix-policy.md` | 신규 | 자동 수정 정책 |
| `Docs/operations/ai-sdlc-append-only-conflict-policy.md` | 신규 | 충돌 완화 정책 |
| `Docs/operations/ai-sdlc-db-migration-roadmap.md` | 신규 | DB 전환 로드맵 |

C# 코드 변경 없음 — 게임 서비스 런타임에 영향 없음.

## 제약 및 주의사항

- **ADR-009 준수**: PostgreSQL SDLC DB 패턴 유지 (psycopg2, SDLC_DB_CONNECTION env)
- **exit 0 원칙**: DB write 실패 / consistency check 불일치가 있어도 SDLC 흐름 차단 금지
- **session-start.sh fallback**: 기존 grep 방식을 유지하여 AI/sprints/ 파일 없을 때도 동작
- **민감 정보 보호**: DB password/connection string을 로그 파일에 기록하지 않음
- **SPRINT.md 보존**: 기존 스프린트 내역(#1~#48) 삭제 금지 — 새 스프린트부터 AI/sprints/ 사용
- **PR 머지 정책**: PR 자동 머지는 영구 제외 — 사람이 직접 검토 후 머지

## 구현 접근 방향

1. `AI/sprints/` 파일 생성 먼저
2. `AI/SPRINT.md` 상단에 인덱스 구조 안내 추가 (기존 내용 보존)
3. `db_write.py`에 실패 로그 함수 추가 (`_log_failure()`) — 각 action 함수에서 호출
4. `check_sdlc_consistency.py` 작성 — psycopg2 연결 실패 시 graceful skip
5. `session-start.sh` 업데이트 — Active Sprint 파일 읽기 + DB 실패 표시
6. `/plan` SKILL.md의 3.2단계 카운터 코드 수정
7. Docs/operations/ 3개 문서 작성
8. `.gitkeep`으로 `AI/logs/db-write-failures/` 디렉터리 생성

## 검증 기준

- `python .github/scripts/check_sdlc_consistency.py --check` → DB 미실행 시 graceful skip, exit 0
- `SDLC_DB_CONNECTION="Host=bad" python .github/scripts/db_write.py --action upsert-job --branch test --status testing` → exit 0 + `AI/logs/db-write-failures/YYYY-MM-DD.log` 생성
- `bash .claude/hooks/session-start.sh` → DB write 실패 로그가 있으면 표시, 없으면 조용히 통과
- `AI/sprints/sprint-049.md` 존재 + `AI/SPRINT.md` 인덱스 링크 포함
- `.claude/skills/plan/SKILL.md` 수정 후 sprint 카운터가 task JSON 파일 수 기반으로 동작
- `dotnet build PlatformA.sln` 성공 (C# 코드 미변경 — 당연히 통과)
