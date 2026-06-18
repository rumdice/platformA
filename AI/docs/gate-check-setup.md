# AI_SDLC Gate Check — n8n 브리지 설정 가이드

Sprint #67(2026-06-18)에서 gate-check가 파일 기반에서 n8n 브리지 방식으로 전환되었다.

## 아키텍처

```
PR 오픈/업데이트
  → GitHub Actions (sdlc-gate-check.yml)
      1. GitHub API: AI_SDLC/gate-check = "pending"
      2. POST n8n webhook: {branch, sha, repo}
  → n8n (로컬 self-hosted)
      1. PostgreSQL 쿼리: sdlc.ai_jobs WHERE branch = ?
      2. 게이트 평가: test_generated, review_completed, impact_done, adr_required
      3. GitHub API: POST /repos/{repo}/statuses/{sha}
         { state: "success"|"failure", context: "AI_SDLC/gate-check" }
  → GitHub PR: commit status 표시 → merge 허용/차단
```

## 전제 조건

### 1. n8n 공개 접근 URL 확보

GitHub Actions는 클라우드 러너에서 실행되므로 `http://localhost:5678`은 접근 불가.
아래 방법 중 하나로 n8n을 외부에서 접근 가능하게 만든다.

**옵션 A: Cloudflare Tunnel (권장 — 무료, 안정적)**

```bash
# 설치
winget install Cloudflare.cloudflared

# 터널 생성 (최초 1회)
cloudflared tunnel login
cloudflared tunnel create platformA-n8n

# 실행 (n8n 시작 후)
cloudflared tunnel --url http://localhost:5678
# → https://xxxxx.trycloudflare.com 형식의 URL이 출력됨
```

**옵션 B: ngrok**

```bash
ngrok http 5678
# → https://xxxx.ngrok.io 형식의 URL이 출력됨
```

> 주의: ngrok 무료 플랜은 세션마다 URL이 변경됨 → Cloudflare Tunnel 권장

### 2. GitHub Secrets 등록

GitHub 저장소 → Settings → Secrets → Actions → New repository secret:

| 이름 | 값 | 설명 |
|------|-----|------|
| `N8N_WEBHOOK_URL` | `https://xxxxx.trycloudflare.com` | n8n 공개 URL (경로 제외) |

### 3. n8n에 GitHub PAT 등록

n8n이 GitHub Commit Status API를 호출하려면 GitHub Personal Access Token이 필요하다.

1. GitHub → Settings → Developer settings → Personal access tokens → Fine-grained tokens
2. Repository access: `rumdice/platformA`
3. Permissions: **Commit statuses** → Read and write
4. n8n 환경변수에 추가 (`docker/n8n/.env` 또는 Docker Compose env):
   ```
   GITHUB_PAT=ghp_xxxxxxxxxxxx
   ```

## n8n 워크플로우 생성

n8n UI (`http://localhost:5678`)에서 아래 흐름을 구성한다.

### 노드 구성

**1. Webhook 노드**
- HTTP Method: POST
- Path: `gate-check`
- Authentication: None (내부 시스템이므로 생략 가능)

**2. PostgreSQL 노드**
```sql
SELECT test_generated, review_completed, impact_done, adr_required, status
FROM sdlc.ai_jobs
WHERE branch = '{{ $json.body.branch }}'
LIMIT 1
```

**3. Code 노드 — 게이트 평가**
```javascript
const job = $('PostgreSQL').first()?.json;
const branch = $('Webhook').first().json.body.branch;

// job이 없는 경우: 고위험 코드 변경이면 fail, 아니면 success
if (!job) {
  return [{
    json: {
      state: 'failure',
      description: `task not found for branch '${branch}' — /plan 실행 필요`,
      context: 'AI_SDLC/gate-check'
    }
  }];
}

// 게이트 평가
if (job.adr_required === true) {
  return [{ json: { state: 'failure', description: '신규 ADR 필요 (/adr 실행)', context: 'AI_SDLC/gate-check' } }];
}
if (job.test_generated === false) {
  return [{ json: { state: 'failure', description: '/test-gen 미실행', context: 'AI_SDLC/gate-check' } }];
}
if (job.impact_done === false) {
  return [{ json: { state: 'failure', description: '/impact 미실행', context: 'AI_SDLC/gate-check' } }];
}
if (job.review_completed === false) {
  return [{ json: { state: 'failure', description: '/review 미실행', context: 'AI_SDLC/gate-check' } }];
}

return [{ json: { state: 'success', description: '모든 게이트 통과', context: 'AI_SDLC/gate-check' } }];
```

**4. HTTP Request 노드 — GitHub Commit Status API**
```
POST https://api.github.com/repos/{{ $('Webhook').first().json.body.repo }}/statuses/{{ $('Webhook').first().json.body.sha }}

Headers:
  Authorization: Bearer {{ $env.GITHUB_PAT }}
  Accept: application/vnd.github+json
  X-GitHub-Api-Version: 2022-11-28

Body (JSON):
{
  "state": "{{ $json.state }}",
  "description": "{{ $json.description }}",
  "context": "{{ $json.context }}"
}
```

워크플로우 생성 후 `.n8n/workflows/` 폴더에 JSON으로 내보내기.

## GitHub Branch Protection Rule 변경

현재 Required status check가 GitHub Actions 워크플로우 체크로 설정된 경우, n8n이 설정하는 커밋 상태로 교체해야 한다.

1. GitHub 저장소 → Settings → Branches → `main` → Edit
2. Required status checks → "Add checks" 검색창에 입력:
   ```
   AI_SDLC/gate-check
   ```
3. 이전 `gate-check` (GitHub Actions job name) 체크가 있으면 제거

> **주의**: `AI_SDLC/gate-check` 상태는 PR이 열린 후 n8n이 응답해야 나타난다.
> n8n이 오프라인이면 status가 `pending`으로 유지되어 PR 머지가 차단된다.

## 롤백 방법

n8n 브리지가 동작하지 않는 경우 즉시 파일 기반으로 롤백할 수 있다.

`.github/workflows/sdlc-gate-check.yml`에서 n8n 스텝들을 아래로 교체:

```yaml
- name: Checkout
  uses: actions/checkout@v4
  with:
    ref: ${{ github.event.pull_request.head.sha }}
    fetch-depth: 0

- name: Get changed files
  id: changed
  run: |
    FILES=$(git diff --name-only origin/main...HEAD)
    echo "files<<EOF" >> $GITHUB_OUTPUT
    echo "$FILES" >> $GITHUB_OUTPUT
    echo "EOF" >> $GITHUB_OUTPUT

- name: Run AI_SDLC Gate Check (Legacy)
  env:
    BRANCH: ${{ github.event.pull_request.head.ref }}
    CHANGED_FILES: ${{ steps.changed.outputs.files }}
    GITHUB_STEP_SUMMARY: ${{ github.step_summary }}
  run: python3 .github/scripts/check_sdlc_gate.py
```

`check_sdlc_gate.py`는 로직 변경 없이 보존되어 있으므로 즉시 동작한다.
