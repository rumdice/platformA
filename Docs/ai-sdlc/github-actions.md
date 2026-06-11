# AI_SDLC GitHub Actions 워크플로

PlatformA AI_SDLC 관련 GitHub Actions 워크플로 목록.

## 워크플로 목록

### ci.yml — Build & Test

- **트리거**: push (main, feature 브랜치), PR
- **실행 내용**: `dotnet build` + `dotnet test`
- **실패 시**: n8n이 10분 이내 감지 → `sdlc.ai_failures` 기록

### docs.yml — DocFX 문서 사이트 배포

- **트리거**: main 머지 후 자동 실행
- **실행 내용**:
  1. `generate_redis_key_docs.py` — Redis 키 문서 자동 생성
  2. `generate_db_schema_doc.py` — DB 스키마 문서 자동 생성
  3. `generate_ai_sdlc_docs.py` — AI SDLC 자동 생성 문서 업데이트
  4. `check_docs_toc.py` — toc 미등재 파일 감지 (비차단)
  5. DocFX 빌드 → GitHub Pages 배포

### sdlc-gate-check.yml — SDLC 게이트 검사

- **트리거**: PR 생성 / 업데이트
- **실행 내용**: DB에서 게이트 플래그 확인 (requirement_done, impact_done, test_generated, review_completed, adr_required)
- **실패 시**: PR 머지 차단

### auto-fix.yml — 자동 수정

- **트리거**: `repository_dispatch` (ai-auto-fix 이벤트)
  - n8n이 CI 실패 감지 후 GitHub API로 트리거
- **실행 내용**: `/qa-failure` 스킬 실행 → 자동 수정 시도
- **제약**: [auto-fix 정책](auto-fix.md) 참조

### sdlc-python-test.yml — Python 스크립트 테스트

- **트리거**: `.github/scripts/` 변경 시
- **실행 내용**: pytest (`.github/scripts/tests/`)

## 배포 아키텍처

```
main 머지
  │
  ├─ ci.yml       ← 빌드/테스트 검증
  │
  └─ docs.yml     ← DocFX 빌드 → GitHub Pages 배포
       └─ [자동 생성 스크립트 3개] → [DocFX] → [Pages]
```

## n8n ↔ GitHub Actions 연동

GitHub Actions는 로컬 PostgreSQL에 직접 접근하지 않는다 (보안 경계).
DB와의 통신은 모두 n8n을 통해 이루어진다:

```
CI 실패 감지
  n8n (로컬, DB 접근 가능)
    └─ Job Lock claim (PostgreSQL)
    └─ repository_dispatch → GitHub Actions
         └─ auto-fix.yml 실행 (DB 접근 없음)
         └─ 결과 → PR comment
    └─ ai_failures INSERT (PostgreSQL)
```

## 관련 문서

- [n8n 실패 모니터](n8n.md)
- [Auto Fix 정책](auto-fix.md)
- [DocFX 빌드 검증](#)
