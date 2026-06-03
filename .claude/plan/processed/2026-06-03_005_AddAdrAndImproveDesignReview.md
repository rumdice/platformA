# 요구사항 명세: AddAdrAndImproveDesignReview

작성일: 2026-06-03
브랜치: 2026-06-03_AddAdrAndImproveDesignReview
소스: plan mode (~/.claude/plans/n8n-docker-cheeky-allen.md)

## 요구사항 요약

스프린트 #39에서 DESIGN_REVIEW 오판으로 누락된 ADR-008(n8n)·ADR-009(PostgreSQL)를 소급 생성한다.
동시에 `/requirement` SKILL.md의 DESIGN_REVIEW 5단계에 기술 도입 체크리스트를 추가하여
"C# 코드 변경 없음 → ADR 불필요" 오판이 재발하지 않도록 워크플로를 강화한다.

## 상세 요구사항

1. **`AI/adr/008-n8n-event-orchestrator.md` 신규 생성**
   - 상태: 확정, 날짜: 2026-06-03
   - 맥락: AI_SDLC Phase 3 이벤트 자동화 워크플로 엔진 필요
   - 결정: n8n 채택 (self-hosted, PostgreSQL 백엔드, Docker Compose 배포)
   - 대안 기각: Temporal(운영 복잡), Airflow(Python 전용), Step Functions(벤더 락인), C# 직접 구현(유지보수 부담)
   - 이득·비용 포함

2. **`AI/adr/009-postgresql-sdlc-db.md` 신규 생성**
   - 상태: 확정, 날짜: 2026-06-03
   - 맥락: SDLC 전용 OLTP DB 필요, 게임 서비스 MariaDB와 격리
   - 결정: PostgreSQL 16 채택 (SDLC 전용 독립 인스턴스, n8n 스키마 분리)
   - 대안 기각: MariaDB 재사용(혼재·n8n 비권장), SQLite(멀티 컨테이너 불가), Redis(영속성 부족)
   - 이득·비용 포함

3. **`.claude/skills/requirement/SKILL.md` 5단계 DESIGN_REVIEW 개선**
   - 기존 "검토 항목" 앞에 `⚠️ 기술 도입 체크리스트` 블록 삽입
   - 4가지 질문 중 하나라도 YES면 무조건 `📝 신규 ADR 필요` 판정
   - 판단 기준 요약 문장 추가: "ADR 필요 여부는 C# 코드 변경이 아닌 기존 아키텍처에 없던 설계 결정이 포함되는가로 판단한다"

## 영향 범위 (예상)

| 파일 | 변경 유형 |
|------|----------|
| `AI/adr/008-n8n-event-orchestrator.md` | 신규 생성 |
| `AI/adr/009-postgresql-sdlc-db.md` | 신규 생성 |
| `.claude/skills/requirement/SKILL.md` | 수정 (5단계 체크리스트 추가) |

C# 소스 코드·인프라 변경 없음.

## 제약 및 주의사항

- ADR 파일명은 3자리 번호 + kebab-case 포맷 (`AI/adr/NNN-kebab-title.md`) 준수
- ADR 상태값: `초안` / `확정` / `대체됨` / `폐기됨` 중 선택 — 소급 생성이므로 `확정` 사용
- SKILL.md 체크리스트는 기존 "검토 항목" 문단 앞에 삽입 (뒤에 추가하면 LLM이 건너뛸 수 있음)
- 기존 ADR 001~007 파일 변경 없음

## 구현 접근 방향

1. ADR 파일 두 개를 `AI/adr/` 에 직접 Write
2. SKILL.md의 `검토 항목:` 라인 바로 앞에 체크리스트 블록 Edit 삽입
3. 세 파일 한 번에 커밋

## 검증 기준

- [ ] `ls AI/adr/ | grep "008\|009"` → 두 파일 존재
- [ ] ADR-008 내용: 맥락·결정·대안(4개)·이득·비용 섹션 완비
- [ ] ADR-009 내용: 맥락·결정·대안(4개)·이득·비용 섹션 완비
- [ ] SKILL.md DESIGN_REVIEW 섹션에 `⚠️ 기술 도입 체크리스트` 블록 노출
- [ ] 체크리스트 4개 항목 모두 포함 (외부서비스·저장소·컴포넌트·통신패턴)
