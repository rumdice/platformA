# 요구사항 명세: AutomateWorkflowPipeline

작성일: 2026-06-05
브랜치: 2026-06-05_AutomateWorkflowPipeline
소스: plan mode (~/.claude/plans/1-tender-cupcake.md)

## 요구사항 요약

사람은 계획 파일 제공과 PR 검수·머지만 담당하고, 나머지 전체 워크플로(plan → coding → test → PR)는 Claude가 완전 자동으로 수행하도록 현재 스킬 시스템의 차단 요인을 제거하고 오케스트레이터를 신규 생성한다.

## 상세 요구사항

### R1. `/done` disable-model-invocation 해제

**문제**: `disable-model-invocation: true`로 Claude가 Skill 도구로 `/done`을 호출할 수 없음.
**해결**: `disable-model-invocation: false`로 변경. 내부 로직(SDLC 게이트 검사, 빌드, 포맷, 테스트, push) 변경 없음.

### R2. `/pr` disable-model-invocation 해제

**문제**: `disable-model-invocation: true`로 Claude가 Skill 도구로 `/pr`을 호출할 수 없음.
**해결**: `disable-model-invocation: false`로 변경. 내부 로직(SPRINT 체크, PR 생성, cost-log, 명세 archived) 변경 없음.

### R3. `/review` allowed-tools 추가 + steps[] 기록

**문제**: `allowed-tools` 미선언으로 Edit 도구를 선언적으로 사용할 수 없음. steps[] 미기록.
**해결**:
- 프론트매터에 `allowed-tools: Bash(git *) Bash(grep *) Read Edit` 추가
- 완료 처리 마지막에 steps[]에 `review` 항목 추가 로직 삽입

### R4. `/start` steps[] 기록 추가

**문제**: status를 coding으로 바꾸지만 steps[]에 start 항목이 기록되지 않음.
**해결**: status 갱신 직후 steps[]에 `start` 항목 추가

### R5. `/test-gen` steps[] 기록 추가

**문제**: `test_generated: true` 플래그만 갱신하고 steps[]에 기록하지 않음.
**해결**: test_generated 갱신 직후 steps[]에 `test_gen` 항목 추가

### R6. `/workflow` 오케스트레이터 신규 생성

**목적**: 전체 파이프라인을 체인하는 메타 스킬. 사람이 `/workflow` 한 번 실행하면 PR 생성까지 자동 완주.

**수행 순서**:
1. 계획 파일 탐지 (`.claude/plan/*.md` 또는 인수 텍스트)
2. `/plan` Skill 호출
3. `/requirement` Skill 호출 (adr_required=true이면 중단)
4. `/impact` Skill 호출
5. `/start` Skill 호출
6. 코딩 (LLM 직접 구현 — 명세 파일 기반)
7. `/test-gen` Skill 호출
8. `/done` Skill 호출 (실패 시 최대 3회 재시도)
9. `/review` Skill 호출
10. `/pr` Skill 호출
11. 완료 보고 (PR URL + 사람이 할 일)

**프론트매터**:
```yaml
name: workflow
disable-model-invocation: false
allowed-tools: Bash(git *) Bash(gh *) Bash(dotnet *) Bash(grep *) Bash(ls *) Bash(python3 *) Read Edit Write Agent
```

### R7. CLAUDE.md 자동화 모드 문서화

Git 워크플로 섹션에 수동 워크플로와 구분되는 자동화 모드 섹션 추가:
```
/workflow {작업설명}  → [자동화 모드] plan부터 pr까지 전체 파이프라인 자동 실행
```

### R8. pipeline.txt 업데이트

스킬 워크플로 섹션에 `/workflow` 추가 및 스킬-단계 매핑 테이블 갱신.

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|------|---------|
| `.claude/skills/done/SKILL.md` | 1줄 변경 (disable-model-invocation) |
| `.claude/skills/pr/SKILL.md` | 1줄 변경 (disable-model-invocation) |
| `.claude/skills/review/SKILL.md` | allowed-tools 추가 + steps[] 로직 추가 |
| `.claude/skills/start/SKILL.md` | steps[] 로직 추가 |
| `.claude/skills/test-gen/SKILL.md` | steps[] 로직 추가 |
| `.claude/skills/workflow/SKILL.md` | 신규 생성 (~150줄) |
| `CLAUDE.md` | 자동화 모드 섹션 추가 |
| `AI/AI_SDLC(pipeline).txt` | /workflow 스킬 추가 |

**게임 서비스 코드 변경 없음. C# 코드 변경 없음.**

## 제약 및 주의사항

1. `/done`·`/pr`의 내부 로직(SDLC 게이트, 빌드 명령, PR 생성)은 변경하지 않는다 — 오직 `disable-model-invocation` 플래그만 변경.
2. `/workflow`는 각 스킬의 실패 시 즉시 중단하고 원인을 보고한다 (무한 루프 방지).
3. `/done` 재시도 최대 3회 — 3회 초과 시 중단 후 수동 수정 안내.
4. ADR 신규 도입 없음 — 스킬 설정 파일 수정이므로 DESIGN_REVIEW 통과.
5. `workreport` 스킬도 `disable-model-invocation: false`임을 확인 — 동일 패턴 적용.

## 구현 접근 방향

```
1. /done SKILL.md: disable-model-invocation true → false (1줄)
2. /pr SKILL.md: 동일 (1줄)
3. /review SKILL.md: 프론트매터에 allowed-tools 추가 + 완료 처리 마지막에 steps[] 추가 명령
4. /start SKILL.md: status 갱신 후 steps[] 추가 명령 삽입
5. /test-gen SKILL.md: test_generated 갱신 후 steps[] 추가 명령 삽입
6. /workflow/SKILL.md: 신규 파일 생성 (메타 스킬)
7. CLAUDE.md: 자동화 모드 섹션 추가
8. pipeline.txt: /workflow 항목 추가
```

## 검증 기준

| 검증 | 방법 | 기대 결과 |
|------|------|---------|
| /done 호출 가능 | Skill 도구로 /done 호출 | 오류 없이 실행 |
| /pr 호출 가능 | Skill 도구로 /pr 호출 | 오류 없이 실행 |
| steps[] 기록 | /start 실행 후 task JSON | steps[]에 start 항목 존재 |
| steps[] 기록 | /test-gen 실행 후 task JSON | steps[]에 test_gen 항목 존재 |
| steps[] 기록 | /review 실행 후 task JSON | steps[]에 review 항목 존재 |
| /workflow 완주 | 간단한 작업 설명으로 실행 | PR 생성까지 자동 완주 |
