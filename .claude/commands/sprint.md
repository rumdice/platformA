# 현재 스프린트 상태 조회

Phase C 기준: DB(`db_write.py --action list-active`)를 우선 조회하고, 실패 시 `AI/sprints/*.md` frontmatter로 폴백한다.

`$ARGUMENTS`가 있으면 해당 키워드(예: `Auth`, `Migration`)와 관련된 항목을 필터링한다.

---

## 수행 순서

### 1단계: DB에서 active sprint 조회

```bash
python .github/scripts/db_write.py --action list-active 2>/dev/null
```

DB에서 active/coding 상태 job 목록을 가져온다.
출력이 비어 있거나 DB 연결 실패 시 2단계 폴백으로 이동한다.

### 2단계: (폴백) AI/sprints/*.md frontmatter 조회

DB 조회 실패 시 아래 명령으로 in-progress 스프린트를 찾는다.

```bash
grep -rl "^status: in-progress" AI/sprints/ 2>/dev/null | sort | tail -5
```

찾은 파일들의 frontmatter(sprint, title, branch, date, status)를 출력한다.

### 3단계: 현재 스프린트 분석

가장 최근(번호가 가장 큰) 진행 중 스프린트를 찾아 아래 항목을 출력한다:

```
## 스프린트 #N — <목표>

### 진행 중
- [ ] <항목>   ← 미완료 항목 전체

### 완료
- [x] <항목>   ← 완료 항목 전체 (5개 이하일 경우 전체, 초과 시 최근 3개만)

### 대기
- [ ] <항목>   ← 대기 항목 (있을 경우)
```

스프린트 상세는 `AI/sprints/sprint-NNN.md`에서 읽는다.

`$ARGUMENTS`가 있으면 키워드와 관련 없는 항목은 제외한다.

### 4단계: 권고사항

진행 중인 스프린트가 **없으면**:
> "현재 진행 중인 태스크가 없습니다. `/plan <작업 설명>`으로 새 태스크를 시작하세요."

진행 중 항목이 있으면:
> 현재 작업 상태를 한 줄로 요약하고, 다음으로 해야 할 구체적인 액션을 안내한다.
