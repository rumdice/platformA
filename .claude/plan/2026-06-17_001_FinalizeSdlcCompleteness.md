# 요구사항 명세: FinalizeSdlcCompleteness

작성일: 2026-06-17
브랜치: 2026-06-17_FinalizeSdlcCompleteness
소스: 직접 입력 (workflow 인수)

## 요구사항 요약

AI SDLC 인프라의 미완성 항목 4가지를 처리하여 완성도를 93%에서 ~100%로 끌어올린다.
C# 코드 변경 없이 DB 레코드 수정, ADR 문서 작성, Python 유닛 테스트, Docs 업데이트로 구성된다.

## 상세 요구사항

### 1. 미해결 CI 실패 DB 처리 (DB 업데이트)

`sdlc.ai_failures` 테이블에서 `test-n8n` 브랜치의 미해결 실패 2건(id=3, id=5)을
`resolved=true`, `resolved_at=NOW()`로 업데이트한다.

배경: test-n8n은 n8n 워크플로 테스트용 임시 브랜치로 이미 삭제됨.
해당 실패는 실제 수정 대상이 아니므로 resolved 처리가 올바른 처리.

### 2. ADR-010 작성 (문서 생성)

`AI/adr/010-phase-c-db-only-source-of-truth.md` 파일을 생성한다.

내용:
- 결정 일자: 2026-06-10 (Phase C 채택 시점)
- 결정 내용: AI SDLC 상태의 단일 진실원을 PostgreSQL DB로 통일 (SPRINT.md, cost-log.md 파일 제거)
- 배경: 파일 기반 상태(AI/SPRINT.md, AI/cost-log.md)는 병렬 작업 시 충돌 가능성, 히스토리 조회 불편, Git 노이즈 발생
- 영향: GitHub Actions는 DB에 직접 접근 금지, n8n을 통한 이벤트 신호만 허용
- 결과: ai_jobs, ai_job_steps, ai_failures, ai_model_runs 4개 테이블이 전체 SDLC 상태 관리
- 기존 ADR-009(PostgreSQL SDLC DB)와의 관계: ADR-009는 DB 도입 결정, ADR-010은 DB로의 완전 전환(파일 제거) 결정

### 3. Python 유닛 테스트 추가 (새 파일 생성)

`.github/tests/` 디렉토리를 생성하고 2개 테스트 파일을 작성한다.

#### 3-A. `test_record_failure.py`

테스트 대상: `record_failure.py`의 핵심 함수
- `parse_conn()` — 연결 문자열 파싱 정확성
- `list_unresolved()` — Mock psycopg2로 DB 반환값 검증
- `record()` — INSERT 파라미터 구성 검증
- `resolve()` — UPDATE 파라미터 구성 검증

Mock 전략: `unittest.mock.patch('psycopg2.connect')` 로 실제 DB 연결 없이 테스트.

#### 3-B. `test_count_tokens.py`

테스트 대상: `count_tokens.py`의 핵심 함수
- `get_project_dir()` — 경로 해시 계산 로직
- `sum_tokens()` 또는 동등 함수 — JSONL 파싱 + 토큰 합산 로직
- edge case: 빈 파일, 필드 누락, 날짜 필터링

Mock 전략: `tmp_path` fixture (pytest) 또는 `tempfile` (unittest)으로 JSONL 임시 파일 생성.

### 4. `Docs/ai-sdlc/auto-fix.md` 업데이트 (문서 수정)

Sprint #63(PR #95)에서 `auto-fix.yml`이 삭제되었으나 문서가 삭제 이전 내용 그대로.
현재 상태를 반영하여 다음을 업데이트한다:

- 제목/목적: "auto-fix.yml 정책" → "CI 실패 감지 및 수동 수정 정책"으로 변경
- `auto-fix.yml`이 삭제된 사실과 이유 명시
- 현재 CI 실패 처리 흐름 반영: n8n 감지 → DB INSERT → session-start.sh 알림 → 수동 수정
- failure_type별 정책 테이블: 자동 수정 컬럼 모두 "No"로 업데이트 (auto-fix 제거)
- Sprint #63 참조 추가

## 영향 범위 (예상)

| 파일/경로 | 변경 유형 |
|----------|---------|
| `sdlc.ai_failures` (DB) | UPDATE — id=3, id=5 resolved=true |
| `AI/adr/010-phase-c-db-only-source-of-truth.md` | 신규 생성 |
| `.github/tests/test_record_failure.py` | 신규 생성 |
| `.github/tests/test_count_tokens.py` | 신규 생성 |
| `Docs/ai-sdlc/auto-fix.md` | 수정 |

C# 코드(.cs, .csproj, .proto) 변경 없음.

## 제약 및 주의사항

- DB UPDATE는 `sdlc.ai_failures`에만 영향 — 다른 테이블 변경 없음
- ADR-010은 소급 문서화 — 신규 아키텍처 결정 아님
- Python 테스트는 `unittest` 표준 라이브러리 사용 (pytest 추가 설치 불필요)
- C# 빌드/테스트 영향 없음
- `sdlc-python-test.yml` 워크플로가 `.github/tests/` 를 대상으로 실행하는지 확인 필요

## 구현 접근 방향

1. **DB 업데이트**: `psycopg2` 직접 쿼리 또는 `record_failure.py --resolve` CLI
2. **ADR 작성**: 기존 ADR 형식(009) 참고하여 마크다운 직접 작성
3. **Python 테스트**: `unittest.mock.patch` + `MagicMock`으로 DB 연결 없이 로직만 검증
4. **Docs 업데이트**: 현재 auto-fix.md 읽고 내용 반영

## 검증 기준

- [ ] `python3 -c "import psycopg2; ... SELECT resolved FROM sdlc.ai_failures WHERE id IN (3,5)"` → 모두 `true`
- [ ] `ls AI/adr/010-*.md` 파일 존재
- [ ] `python3 -m pytest .github/tests/ -v` 또는 `python3 -m unittest discover .github/tests/` → 전체 통과
- [ ] `Docs/ai-sdlc/auto-fix.md` 파일에 "auto-fix.yml 삭제" 문구 포함
- [ ] `sdlc-python-test.yml` CI에서 신규 테스트 통과 확인 (push 후)
