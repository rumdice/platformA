# AI 작업 비용 로그

추적 목적: Phase 3(PostgreSQL 기반 모니터링) 도입 전 기준선 파악.  
Phase 3 전환 시 이 파일을 `ai_model_runs` 테이블로 마이그레이션한다.

## 작업 규모 기준

| 코드 | 기준 |
|------|------|
| **S** | 1-2 파일 변경, 단일 태스크 |
| **M** | 3-10 파일 변경 또는 2-4 태스크 |
| **L** | 10+ 파일 변경 또는 5+ 태스크 |
| **XL** | 전체 서비스 추가/마이그레이션 수준 |

## 기록 방법

`/done` 스킬 실행 시 PR 생성 후 아래 테이블에 행을 추가한다.

---

## 비용 로그

| 날짜 | 스프린트 | 작업명 | 모델 | 규모 | duration_sec | consume_tokens | 비고 |
|------|---------|-------|------|------|-------------|---------------|------|
| 2026-05-18 | #21 | AISDLCEnhancements | claude-sonnet-4-6 | L | — | — | PDF 갭 해소 5종 (스킬/테스트/구조화) |
| 2026-05-21 | #22 | AddPipelineSkills | claude-sonnet-4-6 | M | — | — | /impact·/requirement 스킬 추가, 상태 머신 6단계, /done 비용 자동 계산 |
| 2026-05-21 | #23 | RefactorSdlcSkills | claude-sonnet-4-6 | M | — | — | /start·/pr 신규, /done BUILD_GATE 분리, /requirement 경로·소스탐지 개선 |
| 2026-05-22 | #24 | AddTestGenSkill | claude-sonnet-4-6 | M | — | — | /test-gen 신규, /done 데드코드 제거, SCHEMA 갱신, /review 완료 처리 추가 |
| 2026-05-22 | #24 | FixTestWriterDoc | claude-sonnet-4-6 | S | — | — | test-writer.md — Ticketing/Matching 테스트 현황 반영 및 팩토리 패턴 추가 |
| 2026-05-22 | #26 | AddPrMergeSyncWorkflow | claude-sonnet-4-6 | M | — | — | feat: PR 머지 자동 감지 — GitHub Actions SDLC Task Sync 워크플로우 |
| 2026-05-26 | #27 | SdlcGateCheckAndReport | claude-sonnet-4-6 | M | — | — | feat: GitHub Actions gate check 이중화 + 주간 리포트 스크립트 (PR #55) |
