# 요구사항 명세: AddProjectReadme

작성일: 2026-07-08
브랜치: 2026-07-08_AddProjectReadme
소스: plan mode (7-1-flickering-cook.md)

## 요구사항 요약
PlatformA GitHub 저장소 루트에 영문 README.md를 신규 생성한다.
방문자가 5분 안에 프로젝트 목적·서비스 구성·기술 스택·실행 방법을 파악할 수 있어야 한다.

## 상세 요구사항

1. **언어**: 영문 (GitHub 공개 저장소 표준)
2. **섹션 구성** (순서대로):
   - 프로젝트 헤더: .NET 버전 뱃지, 라이센스 뱃지
   - 한 문장 소개 + 핵심 특징 3가지 bullet
   - 아키텍처 다이어그램: Mermaid flowchart (서비스 간 연결 흐름)
   - 서비스 목록 표 (포트, 프로토콜, 역할)
   - 기술 스택 목록 (런타임, 데이터, 통신, 보안, 인프라, 테스트)
   - 빠른 시작 (Prerequisites → Redis 클러스터 → DB Migration → 서비스 실행)
   - API 문서 링크 (Docs/api-guide/)
   - 테스트 실행 (dotnet test)
   - 프로젝트 구조 (디렉토리 트리 요약)
   - 기여 가이드 (브랜치 워크플로 1줄)
3. **제약**:
   - 이미지·스크린샷 없음 (유지보수 부담)
   - 코드 블록은 실제 실행 가능한 명령어만
   - 복사-붙여넣기로 바로 동작해야 함

## 영향 범위 (예상)

| 파일 | 변경 종류 |
|------|---------|
| `README.md` (저장소 루트) | 신규 생성 |

코드 변경 없음. 신규 파일 1개.

## 제약 및 주의사항

- ADR 충돌 없음 (문서 파일, 아키텍처 결정 없음)
- Mermaid는 GitHub에서 네이티브 지원 (별도 플러그인 불필요)
- 빠른 시작의 실행 명령은 CLAUDE.md 로컬 실행 순서와 동일해야 함
- 포트 정보는 Consts.cs 기반 확인값 사용

## 구현 접근 방향

1. 저장소 루트에 `README.md` 신규 작성
2. Mermaid `flowchart LR`으로 클라이언트 → 서비스 흐름 표현
   - 클라이언트 → Auth.API → Ticketing.API → Matching.API → Game.Lobby → Game.Gomoku
   - Redis Cluster와 MySQL은 하위 데이터 레이어로 표현
3. 서비스 목록 표: 6개 서비스 (포트·프로토콜·역할)
4. 빠른 시작: docker-compose(Redis) → dotnet ef → dotnet run 순서
5. 테스트: `dotnet test PlatformA/PlatformA.sln` 한 줄

## 검증 기준

- `README.md` 파일이 저장소 루트에 존재한다
- GitHub 페이지(`rumdice/platforma`)에서 렌더링이 정상이다
- Mermaid 다이어그램이 GitHub에서 시각적으로 표시된다
- 빠른 시작의 명령어가 실제 실행 가능하다 (CLAUDE.md와 일치)
- Docs/api-guide/ 링크가 저장소 내 유효한 경로를 가리킨다
