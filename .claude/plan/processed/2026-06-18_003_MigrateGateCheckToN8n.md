# 요구사항 명세: MigrateGateCheckToN8n

작성일: 2026-06-18
브랜치: 2026-06-18_MigrateGateCheckToN8n
소스: /workflow 인수 텍스트 + sprint-067.md

## 요구사항 요약

GitHub Actions의 gate-check를 파일 기반(`check_sdlc_gate.py`)에서 n8n 브리지 방식으로 전환한다.
GitHub Actions는 n8n webhook으로 신호만 보내고, n8n이 로컬 PostgreSQL에서 게이트 상태를 조회하여 GitHub Commit Status API로 결과를 설정한다.
이를 통해 Phase C(DB-only) 스프린트에서 task JSON 부재로 발생하는 false-fail을 영구 해소한다.

## 문제 진단

**현재 구조**:
```
PR 오픈 → GitHub Actions → check_sdlc_gate.py → AI/tasks/*.json 탐색
                                                 ↑ Phase C에는 파일 없음 → FAIL
```

**실패 조건**: Phase C 스프린트는 task JSON을 생성하지 않는다(DB-only). `check_sdlc_gate.py`는
파일만 읽으므로 Phase C의 고위험 코드 변경 PR은 항상 FAIL.

**근본 원인**: GitHub Actions 클라우드 러너는 로컬 PostgreSQL에 접근 불가 (CLAUDE.md 원칙).

## 상세 요구사항

### 1. `sdlc-gate-check.yml` 변경

**Before** (현재):
- Python `check_sdlc_gate.py` 실행 → 파일 기반 gate 검사 → exit code로 Actions 상태 결정

**After** (목표):
1. GitHub Actions가 즉시 `pending` commit status 설정 (`AI_SDLC/gate-check`)
2. n8n webhook으로 `{branch, sha, repo, changed_files}` 전송 (30초 타임아웃)
3. GitHub Actions exit 0 (항상 성공) — gate 판정은 n8n이 별도로 수행

```yaml
# 변경 후 워크플로우 핵심 단계
- name: Set gate-check pending
  run: |
    gh api /repos/${{ github.repository }}/statuses/${{ github.event.pull_request.head.sha }} \
      -f state=pending \
      -f description="AI_SDLC 게이트 체크 진행 중..." \
      -f context="AI_SDLC/gate-check"
  env:
    GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}

- name: Trigger n8n gate-check
  run: |
    PAYLOAD=$(jq -n \
      --arg branch "${{ github.event.pull_request.head.ref }}" \
      --arg sha "${{ github.event.pull_request.head.sha }}" \
      --arg repo "${{ github.repository }}" \
      '{branch: $branch, sha: $sha, repo: $repo}')
    curl -X POST "${N8N_WEBHOOK_URL}/gate-check" \
      -H "Content-Type: application/json" \
      -d "$PAYLOAD" \
      --max-time 30 \
    || echo "⚠️ n8n 응답 없음 — pending 상태 유지 (PR 머지 차단됨)"
  env:
    N8N_WEBHOOK_URL: ${{ secrets.N8N_WEBHOOK_URL }}
```

### 2. n8n 워크플로우 명세

n8n 내부 흐름 (`AI_SDLC/gate-check` 워크플로):

```
[Webhook 수신]
  → body.branch, body.sha, body.repo 추출
  → PostgreSQL 쿼리:
      SELECT test_generated, review_completed, impact_done, adr_required, status
      FROM sdlc.ai_jobs
      WHERE branch = '{{body.branch}}'
  → 결과 평가:
      * job 없음 → state = "failure", desc = "task not found (비고위험이면 경고만)"
      * adr_required=true → state = "failure", desc = "신규 ADR 필요"
      * test_generated=false → state = "failure", desc = "/test-gen 미실행"
      * impact_done=false → state = "failure", desc = "/impact 미실행"
      * review_completed=false → state = "failure", desc = "/review 미실행"
      * 전부 통과 → state = "success", desc = "모든 게이트 통과"
  → GitHub API POST:
      POST https://api.github.com/repos/{{body.repo}}/statuses/{{body.sha}}
      { state, description, context: "AI_SDLC/gate-check" }
```

### 3. `check_sdlc_gate.py` 처리

- 삭제하지 않고 상단에 deprecation 주석 추가:
  ```python
  # DEPRECATED: Sprint #67에서 n8n 브리지로 대체됨 (2026-06-18)
  # 이 파일은 롤백 목적으로 유지됨. 새 gate-check는 n8n을 통해 동작함.
  ```
- `sdlc-gate-check.yml`에서 이 스크립트를 더 이상 호출하지 않음
- Python 테스트(`sdlc-python-test.yml`)에서 이 파일의 gate 테스트는 유지 (유닛 테스트는 독립)

### 4. GitHub Branch Protection Rule 가이드

`AI/docs/gate-check-setup.md`에 설정 방법 문서화:
- Branch protection: Required status checks에 `AI_SDLC/gate-check` 추가
- n8n webhook URL을 GitHub Secrets `N8N_WEBHOOK_URL`로 등록
- n8n에서 GitHub PAT 설정 방법

### 5. 전제 조건 (사람이 할 일)

- n8n이 외부에서 접근 가능한 URL에서 실행되어야 함
  - 옵션 A: Cloudflare Tunnel (`cloudflared tunnel --url http://localhost:5678`)
  - 옵션 B: ngrok (`ngrok http 5678`)
  - 옵션 C: 클라우드 배포 (Render, Railway 등)
- GitHub Secrets에 `N8N_WEBHOOK_URL` 등록
- n8n에 GitHub PAT (scope: `repo:status`) 환경변수로 등록

## 영향 범위 (예상)

| 파일 | 변경 유형 | 위험도 |
|------|---------|--------|
| `.github/workflows/sdlc-gate-check.yml` | 수정 | MEDIUM — CI 워크플로우 변경 |
| `.github/scripts/check_sdlc_gate.py` | Deprecation 주석 추가 | LOW |
| `AI/docs/gate-check-setup.md` | 신규 | LOW — 문서 |

**.n8n/workflows/gate-check.json**: n8n UI에서 생성 후 export (코드베이스에 추가)

## 제약 및 주의사항

1. **n8n 가용성**: n8n이 다운되면 PR의 commit status가 `pending`으로 유지 → 머지 차단
   - 이는 의도된 동작 (fail-safe) — gate check는 반드시 통과해야 함
2. **기존 branch protection**: 현재 GitHub Actions 워크플로우 체크가 required status로 등록된 경우
   → `AI_SDLC/gate-check` (n8n 커밋 상태)로 교체 필요
3. **비고위험 문서 PR**: n8n이 DB에서 job을 찾지 못하면 changed_files를 분석하여
   코드 변경이 없으면 자동 PASS 처리 (check_sdlc_gate.py 기존 로직 그대로)
4. **로컬 n8n URL 문제**: `http://localhost:5678`은 GitHub Actions에서 접근 불가 — 반드시 공개 URL 사용
5. **ADR-010 준수**: Phase C는 DB-only source of truth → n8n은 DB를 신뢰하고 파일을 읽지 않음

## 구현 접근 방향

1. `sdlc-gate-check.yml` — `check_sdlc_gate.py` 호출 스텝을 webhook 호출로 교체
2. `check_sdlc_gate.py` — deprecated 주석 추가 (삭제하지 않음)
3. n8n 워크플로우 — 수동으로 n8n UI에서 생성 후 `.n8n/workflows/gate-check.json`으로 export
4. `AI/docs/gate-check-setup.md` — 브랜치 보호 규칙 설정 가이드 작성

**구현 우선순위**:
- 1순위: `sdlc-gate-check.yml` 변경 (핵심)
- 2순위: `check_sdlc_gate.py` deprecated 주석
- 3순위: 문서화

## 검증 기준

1. `sdlc-gate-check.yml`에서 `check_sdlc_gate.py` 호출 스텝이 제거됨
2. n8n webhook 호출 스텝이 추가됨 (pending status setter 포함)
3. `check_sdlc_gate.py` 상단에 DEPRECATED 주석이 추가됨
4. `AI/docs/gate-check-setup.md`가 생성됨
5. 빌드(`dotnet build`)가 영향받지 않음 (Python/YAML 변경만)
6. `sdlc-python-test.yml`의 기존 테스트가 여전히 통과함
