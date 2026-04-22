# SPRINT — 현재 스프린트

> AI는 세션 시작 시 이 파일을 가장 먼저 읽습니다.
> 작업 완료 즉시 체크박스를 업데이트하십시오.

---

## 스프린트 #1 (2026-04-21) — 완료
**목표**: AI 자율 개발 기반 문서 체계 구축

- [x] CLAUDE.md + AI/ 하위 문서 12개 작성
- [x] .claude/settings.json 프로젝트 설정 생성
- [x] .claude/hooks/pre-push-build-check.sh 빌드 검증 hook

---

## 스프린트 #2 (2026-04-22) — 완료
**목표**: Utils.API 유닛/통합 테스트 구축

- [x] AI/TESTING_STRATEGY.md Utils.API 명세 추가
- [x] FactAttribute 섀도잉 버그 수정
- [x] Program.cs IConnectionMultiplexer DI 버그 수정
- [x] Base62ConverterTests (7개) 작성
- [x] SnowflakeGeneratorTests (7개) 작성
- [x] UtilControllerTests 통합 테스트 (9개) 작성

---

## 스프린트 #3 (진행 중)
**목표**: 폴더 구조 정비 및 프로젝트 설정 안정화

### 진행 중
- [x] docs/ → AI/ 폴더명 변경 및 전체 참조 업데이트
- [x] .claude/settings.json 프로젝트 전용 생성
- [ ] README.md 프로젝트 실제 내용으로 작성
- [ ] BACKLOG 항목 우선순위 합의

---

## 대기 (다음 스프린트 후보)

- [ ] #BACK-001: 설정값 환경변수 이전 (JWT 시크릿, DB 비밀번호)
- [ ] #BACK-002: Matching API Dockerfile 추가
- [ ] #BACK-003: 전체 스택 docker-compose.yml
- [ ] #BACK-004: Auth/Matching API 유닛 테스트
