# SPRINT — 현재 스프린트

> AI는 세션 시작 시 이 파일을 가장 먼저 읽습니다.
> 작업 완료 즉시 체크박스를 업데이트하십시오.

---

## 스프린트 #1 (2026-04-21 ~)
**목표**: AI 자율 개발 기반 문서 체계 구축 및 코드베이스 안정화

---

## 진행 중

(없음)

---

## 완료

- [x] CLAUDE.md 작성 (AI 운영 지침서)
- [x] docs/ARCHITECTURE.md 작성 (시스템 설계 문서)
- [x] docs/adr/001-redis-cluster.md 작성
- [x] docs/adr/002-binary-packet-protocol.md 작성
- [x] docs/adr/003-hardcoded-config.md 작성
- [x] docs/RUNBOOK.md 작성 (빌드/배포 명령)
- [x] docs/ENVIRONMENT.md 작성 (환경 설정)
- [x] docs/API_CONTRACTS.md 작성 (API 명세)
- [x] docs/DOMAIN.md 작성 (비즈니스 규칙)
- [x] docs/TESTING_STRATEGY.md 작성 (테스트 전략)
- [x] docs/PATTERNS.md 작성 (코딩 패턴)
- [x] docs/SPRINT.md + docs/BACKLOG.md 작성

---

## 대기

- [ ] `dotnet build PlatformA.sln` 빌드 오류 없음 최종 검증
- [ ] BACKLOG 항목 중 우선순위 합의 (#BACK-001 ~ #BACK-006)

---

## 스프린트 #2 (2026-04-22 ~)
**목표**: Utils.API 유닛 테스트 자동화 구축 및 검증

### 진행 중

- [ ] Utils.API 유닛 테스트 명세 작성 (`docs/TESTING_STRATEGY.md` 업데이트)
- [ ] 테스트 프로젝트 설정 수정 (FactAttribute 버그 제거, 패키지 추가)
- [ ] `Base62ConverterTests` 작성 및 통과
- [ ] `SnowflakeGeneratorTests` 작성 및 통과
- [ ] `UtilControllerTests` (통합) 작성 및 통과
- [ ] `dotnet test` 전체 통과 확인
