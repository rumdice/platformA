# ADR-010: Phase C — PostgreSQL DB 단독 진실원 (파일 기반 상태 제거)

## 상태: 확정

## 날짜: 2026-06-10

---

## 맥락

ADR-009(PostgreSQL SDLC DB 채택) 이후 AI SDLC는 두 가지 상태 저장 방식이 병존했다:

1. **파일 기반 상태**: `AI/SPRINT.md`, `AI/cost-log.md` — 스프린트 진행 상황과 비용 요약
2. **DB 기반 상태**: `sdlc.ai_jobs`, `sdlc.ai_job_steps`, `sdlc.ai_failures`, `sdlc.ai_model_runs` — 동일 정보의 DB 복사본

이 이중화 구조는 다음 문제를 야기했다:

- **충돌 위험**: 동일 정보가 파일과 DB에 별도로 존재 → 불일치 발생 가능
- **Git 노이즈**: 스프린트 진행마다 `AI/SPRINT.md` 자동 커밋이 PR diff를 오염
- **GitHub Actions 격리 위반**: `pr-merge-sync.yml`이 `generate_sprint_md.py`를 호출하여
  로컬 PostgreSQL에 접근 시도 → 클라우드 runner에서 실패 (격리 원칙 위반)
- **유지보수 부담**: 파일 업데이트 로직을 별도로 관리해야 함

---

## 결정

**Phase C: PostgreSQL DB를 AI SDLC 상태의 단일 진실원으로 확정하고 파일 기반 상태를 완전히 제거한다.**

### 제거 대상

| 파일 | 제거 이유 |
|------|---------|
| `AI/SPRINT.md` | DB `sdlc.ai_jobs` + `AI/sprints/sprint-NNN.md`로 대체 |
| `AI/cost-log.md` | DB `sdlc.ai_model_runs` + `AI/reports/` 생성 보고서로 대체 |
| `.github/scripts/generate_sprint_md.py` | 대상 파일 없음 + DB 직접 접근 코드 포함 |
| `.github/workflows/auto-fix.yml` | 별도 ANTHROPIC_API_KEY 과금 + DB 격리 원칙 위반 |

### 유지 대상

| 컴포넌트 | 역할 |
|---------|------|
| `AI/sprints/sprint-NNN.md` | 사람이 읽는 스프린트 요약 (Git 히스토리 보존 목적) |
| `AI/workreport/YYYY-MM-DD.md` | 일일 작업 리포트 (main 브랜치 직접 push 허용) |
| `AI/reports/` | DB 기반 자동 생성 보고서 |
| `AI/adr/` | 아키텍처 결정 기록 |

### DB 단독 진실원 원칙

```
모든 SDLC 상태 읽기·쓰기 = PostgreSQL sdlc 스키마
                         ↑
            GitHub Actions는 접근 금지
                 n8n을 통한 이벤트 신호만 허용
```

---

## 결과

### 장점

1. **단일 진실원**: 상태 불일치 불가 — DB가 항상 최신
2. **CI 격리 준수**: GitHub Actions(클라우드 VM)에서 로컬 DB 접근 시도 코드 없음
3. **Git 노이즈 제거**: 스프린트 진행이 파일 커밋을 생성하지 않음
4. **원자적 조회**: `db_write.py --action get-gates`로 모든 게이트 상태를 단일 쿼리로 조회
5. **이력 관리**: `sdlc.ai_job_steps`에 모든 단계 타임스탬프 기록

### 단점 / 리스크

1. **로컬 DB 의존**: PostgreSQL 미실행 시 `/plan`, `/done`, `/pr` 모두 차단
   → 완화: `docker/postgresql/docker-compose.yml`로 한 명령 재시작 가능
2. **DB 백업 책임**: `backup_sdlc_db.sh` 주기적 실행 필요
3. **GitHub Actions 직접 상태 조회 불가**: Actions에서는 파일 기반 상태만 읽을 수 있음
   → `AI/sprints/sprint-NNN.md` frontmatter가 Actions용 최소 상태 제공

---

## 관련 ADR

- **ADR-009** (PostgreSQL SDLC DB 채택): DB 인프라 도입 결정 — Phase C의 전제 조건
- **ADR-008** (n8n 이벤트 오케스트레이터): GitHub Actions ↔ DB 브리지 역할

## 관련 스프린트

- **Sprint #57** (PR #88): Phase C 전환 + `AI/SPRINT.md` 삭제
- **Sprint #62** (PR #94): Phase C 이후 파일 참조 코드 정리
- **Sprint #63** (PR #95): `generate_sprint_md.py` 삭제, `auto-fix.yml` 삭제, `pr-merge-sync.yml` DB step 제거
