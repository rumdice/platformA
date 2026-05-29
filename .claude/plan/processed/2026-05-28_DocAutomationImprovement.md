# 요구사항 명세 — 문서화 자동화 개선

**작성일**: 2026-05-28  
**스프린트**: #29  
**브랜치**: 2026-05-28_DocAutomationImprovement  
**상태**: 처리 완료 (소급 복구)

---

## 1. 배경 및 목적

기존 API 문서(`Docs/api-guide/`)와 DB 스키마 문서(`Docs/architecture/database-schema.md`)는
코드 변경 시 수동으로 갱신해야 했다. 이로 인해 문서가 실제 코드와 불일치하는 경우가 발생한다.

**목표**: GitHub Actions `docs.yml` 워크플로에 파이썬 스크립트를 추가하여 PR 머지 시
문서를 자동으로 갱신한다. 또한 Game.Server·MySqlDB.Lib의 XML 주석 및 DocFX 메타데이터를 추가한다.

---

## 2. 요구사항

| ID | 요구사항 | 우선순위 |
|----|----------|----------|
| F-01 | `generate_api_docs.py`: 컨트롤러 XML 주석·DTO·오류 패턴 파싱 → `Docs/api-guide/*.md` 자동 갱신 | P0 |
| F-02 | `generate_db_schema.py`: Entity 클래스 파싱 → `Docs/architecture/database-schema.md` 테이블 명세 섹션 교체 | P0 |
| F-03 | `docs.yml`: 두 스크립트 스텝 추가, MySqlDB.Lib·Game.Server 경로 트리거 확장 | P0 |
| F-04 | `docfx.json`: MySqlDB.Lib·Game.Server csproj 메타데이터 추가 | P1 |
| F-05 | Game.Server 핵심 파일 4개(GameRoom, GameRoomManager, GameSession, PacketHandler) XML 주석 추가 | P1 |
| F-06 | `Docs/developer-guide/game-server-architecture.md` 신규 — TCP 구조·JobQueue·분산락 설계 문서화 | P1 |

---

## 3. 구현 요약

- `generate_api_docs.py`: 정규식으로 컨트롤러·DTO·오류 패턴 파싱, api-guide 4개 자동 생성
- `generate_db_schema.py`: Entity 클래스 파싱, DB 스키마 섹션만 교체 (ER 다이어그램 보존)
- Game.Server·MySqlDB.Lib: `GenerateDocumentationFile=true`, `NoWarn CS1591` 추가
- docfx 빌드: 111개 API 파일 성공

---

## 4. 검증 결과

- `dotnet build PlatformA.sln` — 0 error(s)
- `py generate_api_docs.py` — auth/ticketing/matching/utils 4개 OK
- `py generate_db_schema.py` — 7개 Entity 파싱 OK
- `docfx Docs/docfx.json` — 0 error(s), 111개 API 파일 빌드 성공
