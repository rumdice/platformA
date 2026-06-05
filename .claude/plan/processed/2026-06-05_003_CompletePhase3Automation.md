# 요구사항 명세: CompletePhase3Automation

작성일: 2026-06-05
브랜치: 2026-06-05_CompletePhase3Automation
소스: plan mode (~/.claude/plans/1-tender-cupcake.md)

## 요구사항 요약

Phase 3 자동화의 마지막 조각을 완성한다. `/done` steps[] 누락 보정, 계획 파일 Push 시 GitHub Actions가 `/workflow`를 자동 실행, n8n이 fixable CI 실패를 감지하면 자동 수정 트리거, cost-log 토큰 역산 인프라 구축.

## 상세 요구사항

### 1. `/done` steps[] 기록 추가

- **파일**: `.claude/skills/done/SKILL.md`
- **위치**: `4.5단계: task 상태 → "testing"` 블록 끝
- **추가 내용**: Edit 도구로 status를 "testing"으로 변경하는 동시에 steps[] 배열에 `"name": "done"` 항목 추가
  ```json
  {
    "name": "done",
    "status": "done",
    "completed_at": "{ISO8601 현재 시각}",
    "summary": "빌드: 성공, 테스트: N개 통과, push 완료 — {브랜치명}"
  }
  ```

### 2. GitHub Actions: plan-file-trigger.yml (계획 파일 자동 실행)

- **신규 파일**: `.github/workflows/plan-file-trigger.yml`
- **트리거**: `.claude/plan/*.md` 파일이 push될 때 (processed/ 제외)
- **동작**: ubuntu-latest runner에서 dotnet 설치 → Claude Code CLI 설치 → `claude --dangerously-skip-permissions -p "/workflow"` 실행
- **환경변수**: `ANTHROPIC_API_KEY` (Secrets), `GH_TOKEN` (GITHUB_TOKEN)
- **git 설정**: `claude-code[bot]` 봇 계정으로 커밋

### 3. GitHub Actions: auto-fix.yml (CI 실패 자동 수정)

- **신규 파일**: `.github/workflows/auto-fix.yml`
- **트리거**: `repository_dispatch: types: [ai-auto-fix]`
- **동작**: `client_payload.branch`를 체크아웃 → Claude Code CLI 설치 → `claude --dangerously-skip-permissions -p "/qa-failure --run-id $RUN_ID"` 실행
- `/qa-failure` 스킬이 실패를 분석·수정·`/done` 재실행까지 처리

### 4. n8n 워크플로 확장

- **파일**: `.n8n/workflows/github-failure-monitor.json`
- **추가 노드**: INSERT 이후 두 노드 연결
  - 노드 A (Code): `fixable_by_ai === true` 필터
  - 노드 B (HTTP Request): `POST https://api.github.com/repos/rumdice/platformA/dispatches`로 `ai-auto-fix` dispatch 발행
- **페이로드**: failure_id, failure_type, branch, run_id, message

### 5. AI_SDLC(pipeline).txt 문서 갱신

- Phase3 완성도: "약 55% 완료" → "약 90% 완료"
- 미완 목록에서 완료 항목 이동 (plan-file-trigger, auto-fix)

### 6. cost-log 토큰 역산 인프라

**6-1. `/pr` SKILL.md 수정**: `python3 ... || python ...` → `python ... || python3 ...` (순서 교체)
- 이유: Windows에서 `python3`는 Microsoft Store stub(exit 49)이고 `python`이 실제 3.9.13

**6-2. `count_tokens.py` 디버그 모드 추가**:
```python
if os.environ.get("COUNT_TOKENS_DEBUG"):
    print(f"[DEBUG] python={sys.executable}", file=sys.stderr)
    print(f"[DEBUG] project_dir={get_project_dir()}", file=sys.stderr)
```

**6-3. `backfill_cost_log.py` 신규 생성** (`.github/scripts/backfill_cost_log.py`):
- `--sprint N` 또는 `--sprint-range N M` 인수 수신
- AI/tasks/sprint{N}_*.json에서 created_at 추출
- count_tokens.py 호출로 토큰 계산
- AI/cost-log.md에서 해당 스프린트 행의 `—`를 실제 값으로 교체

## 영향 범위 (예상)

| 파일 | 유형 |
|------|------|
| `.claude/skills/done/SKILL.md` | 수정 |
| `.claude/skills/pr/SKILL.md` | 수정 |
| `.github/scripts/count_tokens.py` | 수정 |
| `.github/scripts/backfill_cost_log.py` | 신규 |
| `.github/workflows/plan-file-trigger.yml` | 신규 |
| `.github/workflows/auto-fix.yml` | 신규 |
| `.n8n/workflows/github-failure-monitor.json` | 수정 |
| `AI/AI_SDLC(pipeline).txt` | 수정 |

## 제약 및 주의사항

- ADR-008(n8n) 및 ADR-009(PostgreSQL) 범위 내 — 신규 ADR 불필요
- `plan-file-trigger.yml`은 `.claude/plan/processed/` 경로를 반드시 제외 (무한 루프 방지)
- `auto-fix.yml`의 `--dangerously-skip-permissions`는 CI 전용 — 로컬 실행 금지
- n8n의 GitHub dispatch에는 `GITHUB_TOKEN` PAT(`repo` + `actions:write`) 필요
- `backfill_cost_log.py`의 토큰 계산은 created_at 이후 모든 세션을 합산 — 정밀도 한계 인지
- C# 코드 변경 없음 → 빌드·테스트 검증 불필요, test_generated=true로 설정 가능

## 구현 접근 방향

1. **done/SKILL.md**: 4.5단계 마지막에 steps[] Edit 블록 추가 (status 교체와 동일 Edit 호출로 묶음)
2. **plan-file-trigger.yml**: on.push.paths 트리거 + dotnet + npm(claude) 설치 + `/workflow` 실행
3. **auto-fix.yml**: on.repository_dispatch + ref 체크아웃 + `/qa-failure` 실행
4. **n8n JSON**: PostgreSQL INSERT 노드의 next 연결을 Code 필터 노드로 변경 후 HTTP dispatch 노드 추가
5. **backfill_cost_log.py**: argparse → task JSON glob → count_tokens subprocess → re.sub로 cost-log 교체

## 검증 기준

- [ ] `/done` 실행 후 task JSON의 steps[]에 `"name": "done"` 항목이 존재한다
- [ ] `.claude/plan/*.md` 파일 push 시 `plan-file-trigger` GitHub Actions가 실행된다
- [ ] n8n 워크플로에 fixable 필터 노드와 GitHub dispatch 노드가 존재한다
- [ ] `auto-fix.yml`이 `repository_dispatch: ai-auto-fix` 이벤트에 반응한다
- [ ] `python .github/scripts/count_tokens.py` 정상 출력 확인
- [ ] `python .github/scripts/backfill_cost_log.py --sprint-range 39 44` 실행 후 cost-log.md 행이 업데이트된다
- [ ] `dotnet build PlatformA.sln` 빌드 오류 없음 (C# 무변경이므로 당연히 통과)
