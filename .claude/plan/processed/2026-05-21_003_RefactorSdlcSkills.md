# 요구사항 명세: RefactorSdlcSkills

작성일: 2026-05-21
소스: plan mode (aws-wiggly-bee.md)
※ 소급 생성 — /requirement 미호출로 인해 구현 완료 후 생성됨

## 요구사항 요약

PR #46 (CleanupUtilsApi) 작업 사이클 평가 결과 식별된 AI_SDLC 0~4단계 파이프라인의
세 가지 문제를 수정한다:
1. `/impact` allowed-tools 버그 (`Bash(ls *)` 누락)
2. `/done` 스킬 과부하 — 3개 파이프라인 단계를 하나로 처리
3. Stage 4 (CODE_FIX) 진입 신호 스킬 부재

## 상세 요구사항

1. `/impact` SKILL.md — `allowed-tools`에 `Bash(ls *)` 추가
   - 브랜치 생성 직후(코드 수정 전) 명세 파일을 읽어 사전 분석하는 기능이 동작하지 않음
   - 원인: `ls` 명령 권한 미부여

2. `/done` SKILL.md 슬림화 — BUILD_GATE(1~5단계)만 담당하도록 축소
   - 현재 9단계(커밋+빌드+포맷+테스트+push+SPRINT+PR+task+cost-log)
   - SPRINT/PR/task/cost-log → `/pr` 스킬로 분리

3. `/pr` SKILL.md 신규 생성 — PR_SUMMARY(Stage 8) 전담
   - SPRINT.md 완료 체크
   - PR 생성 (중복 방지 사전 검사 포함)
   - task JSON 완료 처리 (`status: done`, `completed_at`, `pr_url`)
   - cost-log.md 기록

4. `/start` SKILL.md 신규 생성 — CODE_FIX(Stage 4) 진입점
   - task 상태 `analyzing` → `coding` 전환
   - 현재 브랜치 명세 파일 읽어 작업 지시서 출력

5. `AI/AI_SDLC(pipeline).txt` 업데이트
   - 스킬 워크플로 순서도 추가
   - 단계-스킬 매핑 테이블 추가
   - 4단계 `/start`, 8단계 `/pr` 반영

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|------|---------|
| `.claude/skills/impact/SKILL.md` | 수정 |
| `.claude/skills/done/SKILL.md` | 수정 (슬림화) |
| `.claude/skills/pr/SKILL.md` | 신규 |
| `.claude/skills/start/SKILL.md` | 신규 |
| `AI/AI_SDLC(pipeline).txt` | 수정 |

## 제약 및 주의사항

- `/pr` 스킬에 PR 중복 생성 방지 로직 필수 (이미 오픈 PR이 있으면 중단)
- `/done`은 반복 실행 안전해야 함 (push만 하므로 PR 생성 없음)
- task JSON `status` 전환 흐름: `analyzing`(plan) → `coding`(start) → `testing`(done) → `done`(pr)

## 구현 접근 방향

1. `/impact` allowed-tools 1행 수정
2. `/done` 6~9단계 제거, 완료 보고에 `/pr` 안내 추가
3. `/pr` 스킬 신규 작성 (기존 `/done` 6~9단계 내용 이전)
4. `/start` 스킬 신규 작성 (task JSON + 명세 파일 읽기)
5. `AI_SDLC(pipeline).txt` 워크플로 섹션 추가

## 검증 기준

- 새 워크플로 실행 순서가 문서화됨:
  `/requirement` → `/plan` → `/impact` → `/start` → 코딩 → `/done` → `/pr`
- `/done` 반복 실행 시 PR 중복 생성 없음
- `/impact` 가 코드 변경 전 상태에서 명세 파일 읽어 사전 분석 동작
- task JSON 상태 전환이 각 스킬에서 올바르게 이루어짐
