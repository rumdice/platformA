# AI_SDLC Auto-fix Safety Policy

작성일: 2026-06-08
관련 스프린트: #49

## 1. 목적

AI_SDLC 자동 수정 루프(Phase 3 auto-fix.yml, /qa-failure)의 허용 범위와 금지 범위를 정의한다.
자동 수정은 반복 실행 가능한 저위험 작업에만 적용하고, 높은 위험도나 사람 판단이 필요한 변경은 차단한다.

## 2. 기본 원칙

1. 자동 수정은 기본적으로 LOW risk 작업에 한정한다.
2. HIGH risk 작업은 자동 수정하지 않는다 — 실패를 기록하고 사람에게 알린다.
3. DB schema, auth, security, payment, deployment 변경은 사람 승인 필수다.
4. 자동 수정은 반드시 커밋 diff와 요약을 남긴다.
5. `retry_count`는 최대 3회로 제한한다. 3회 초과 시 자동 수정 중단 + 사람에게 알림.
6. **PR 머지는 자동화하지 않는다** — 항상 사람이 직접 GitHub에서 검토 후 머지한다. (영구 정책)

## 3. failure_type별 정책

| failure_type | 자동 분석 | 자동 수정 | 사람 승인 | 비고 |
|---|:---:|:---:|:---:|---|
| `format_failed` | Yes | **Yes** | No | `dotnet format` whitespace/style 자동 적용 |
| `style_failed` | Yes | **Yes** | No | 문서 스타일, 마크다운 형식 |
| `docs_failed` | Yes | **Yes** | Optional | DocFX 빌드, api-guide 동기화 |
| `build_failed` | Yes | **Limited** | Yes | LOW/MEDIUM risk만 제한적 수정, HIGH는 사람에게 |
| `test_failed` | Yes | No | **Yes** | 테스트 실패 자동 수정은 의도치 않은 로직 변경 위험 |
| `sdlc_gate_failed` | Yes | **Limited** | Yes | 누락된 단계(test-gen, review) 보정 가능, 강제 통과 금지 |
| `db_migration_failed` | Yes | No | **Yes** | DB schema 변경 — 항상 사람이 검토 |
| `security_failed` | Yes | No | **Yes** | 보안 취약점 자동 수정 금지 |
| `deploy_failed` | Yes | No | **Yes** | 환경 의존성 큼, 수동 확인 필요 |

## 4. risk별 정책

| risk (impact.risk) | 자동 수정 적용 | 비고 |
|---|---|---|
| LOW | **허용** | format, style, docs 범위 내 자동 수정 |
| MEDIUM | **제한적 허용** | format/style은 허용, build/test 수정은 사람 확인 |
| HIGH | **금지** | 자동 수정 중단, 사람에게 알림 후 대기 |

## 5. 자동 수정 흐름

```
CI 실패 감지 (GitHub Actions → n8n → repository_dispatch)
  ↓
failure_type 분류
  ↓
risk 확인 (task JSON impact.risk)
  ↓
정책 매트릭스 조회
  ↓
허용: /qa-failure 스킬 실행 → 자동 수정 → push → PR
금지: 실패 기록 + 사람에게 알림
```

## 6. 자동 수정 로그

자동 수정이 발생하면 아래를 기록한다:
- `AI/tasks/sprint*.json`의 `retry_count` 증가
- `AI/tasks/sprint*.json`의 `last_error` 업데이트
- 커밋 메시지에 `auto-fix:` 접두사 포함

## 7. 자동 수정 금지 파일 목록

아래 경로가 변경 대상인 경우 자동 수정하지 않는다:

```
PlatformA.Library/**
**/Migrations/**
**/*Context.cs
**/*Entities/**
**/*Auth*
**/*Token*
**/*Jwt*
**/Consts.cs
.github/workflows/**  (자동 수정 워크플로 자체는 변경 금지)
```

## 8. Job Lock 정책 (Phase C)

n8n auto-fix는 Phase C부터 job lock을 획득해야 작업을 시작할 수 있다.

### lock claim 필수

```bash
python .github/scripts/job_lock.py claim \
  --branch "${BRANCH}" \
  --agent-id "n8n" \
  --ttl 30
```

- lock 획득 실패 시: 해당 PR에 comment만 남기고 작업 중단
  - 메시지 예시: `"자동 수정 보류 — 다른 agent가 작업 중입니다. owner={locked_by}"`
- lock 최대 TTL: **30분** (일반 작업 60분의 절반)
- retry_count >= 3인 작업: lock claim 시도 금지 (수동 처리 필요)
- HIGH risk 작업: lock claim 시도 금지

### 완료 후 release

```bash
python .github/scripts/job_lock.py release \
  --branch "${BRANCH}" --token "${LOCK_TOKEN}"
```

release 실패 시: TTL 만료 대기 (30분 이내 자동 해제)

## 관련 문서

- `AI/AI_SDLC(pipeline).txt`: Phase 3 전체 파이프라인 설명
- `.github/workflows/auto-fix.yml`: 자동 수정 트리거 워크플로
- `Docs/operations/ai-sdlc-db-migration-roadmap.md`: DB 전환 로드맵
- `Docs/operations/ai-sdlc-job-lock-policy.md`: Job Lock 상세 정책
