# AI_SDLC CI 실패 감지 및 수동 수정 정책

작성일: 2026-06-08 | 최종 수정: 2026-06-17 | 관련 스프린트: #49, #63

> **[2026-06-17 업데이트]** Sprint #63(PR #95)에서 `auto-fix.yml`이 삭제되었다.
> AI가 CI 실패를 자동으로 수정하는 워크플로는 더 이상 존재하지 않는다.
> 현재는 n8n이 실패를 감지하여 DB에 기록하고, 사람이 수동으로 수정하는 방식을 사용한다.

## 1. 현재 CI 실패 처리 흐름

```
CI 실패 발생 (GitHub Actions)
  ↓
n8n GitHub CI Failure Monitor (10분 폴링)
  → 커서 기반 폴링 — last_checked_at 이후 실패만 조회
  ↓
sdlc.ai_failures INSERT (n8n → PostgreSQL)
  → ON CONFLICT DO NOTHING (중복 방어)
  ↓
session-start.sh / /workflow 0.5단계
  → 미해결 CI 실패 목록 알림
  ↓
개발자가 해당 브랜치에서 수동 수정 후 /done 재실행
```

n8n 아키텍처 상세: [n8n 문서](n8n.md)

## 2. auto-fix.yml 삭제 이유 (Sprint #63)

Sprint #63(PR #95)에서 `auto-fix.yml`을 삭제한 이유:

1. **별도 API 과금**: `ANTHROPIC_API_KEY`를 GitHub Secret에 별도 등록해야 하며
   클라우드에서 Anthropic API를 직접 호출 → 예상치 못한 비용 발생 가능
2. **DB 격리 원칙 위반**: GitHub Actions runner(클라우드 VM)가 로컬 PostgreSQL에
   접근하려는 구조 → CLAUDE.md에 명시된 격리 원칙 위반
3. **미등록 상태**: `ANTHROPIC_API_KEY`가 등록된 적 없어 실제로 동작한 적 없음
4. **설계 방향**: 자동 수정보다 빠른 감지 + 명확한 사람 개입이 더 안전

## 3. failure_type별 현재 정책

| failure_type | 자동 감지 (n8n) | 자동 수정 | 처리 방법 |
|---|:---:|:---:|---|
| `format_failed` | Yes | **No** | 개발자가 `dotnet format` 후 재push |
| `style_failed` | Yes | **No** | 개발자가 `dotnet format style` 후 재push |
| `docs_failed` | Yes | **No** | 개발자가 `/doc-writer api-guide` 실행 |
| `build_failed` | Yes | **No** | 개발자가 빌드 오류 수정 후 재push |
| `test_failed` | Yes | **No** | 개발자가 테스트 실패 원인 분석 후 수정 |
| `sdlc_gate_failed` | Yes | **No** | 개발자가 누락된 SDLC 단계(/impact, /test-gen 등) 실행 |
| `db_migration_failed` | Yes | **No** | 항상 사람이 직접 검토 |
| `security_failed` | Yes | **No** | 항상 사람이 직접 검토 |
| `deploy_failed` | Yes | **No** | 환경 의존성 큼, 수동 확인 필요 |

## 4. 미해결 실패 확인 방법

```bash
# 특정 브랜치 미해결 실패 조회
python .github/scripts/record_failure.py --list-unresolved --branch {브랜치명}

# 전체 미해결 실패 조회
python .github/scripts/record_failure.py --list-unresolved

# 수동으로 resolved 처리
python .github/scripts/record_failure.py --resolve --branch {브랜치명} --type {failure_type}
```

세션 시작 시 `session-start.sh`가 main 브랜치에서 전체 미해결 목록을 자동 출력한다.
`/workflow` 스킬 0.5단계에서도 작업 시작 전 미해결 실패를 알린다.

## 5. CI 실패 기록 유지 원칙

- `sdlc.ai_failures.resolved = false` → 미처리 상태
- `sdlc.ai_failures.resolved = true` → 처리 완료 (수동 resolved 또는 /done 성공 후 자동 처리)
- 임시 테스트 브랜치나 삭제된 브랜치의 실패는 수동으로 `resolved=true` 처리 가능
- **retry_count** 필드는 현재 사용하지 않음 (auto-fix 제거로 재시도 루프 없음)

## 6. 자동 수정 금지 파일 (참고)

과거 auto-fix 정책에서 자동 수정이 금지되었던 파일 목록 (현재는 어차피 자동 수정 없음):

```
PlatformA.Library/**
**/Migrations/**
**/*Context.cs
**/*Entities/**
**/*Auth*
**/*Token*
**/*Jwt*
**/Consts.cs
.github/workflows/**
```

## 7. 관련 문서

- n8n CI 실패 감지 설정: [n8n 문서](n8n.md)
- Job Lock (현재는 /start에서만 사용): [Job Lock 문서](job-lock.md)
- GitHub Actions 격리 원칙: [GitHub Actions 문서](github-actions.md)
