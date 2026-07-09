# 요구사항 명세: AutoArchitectureDocs

작성일: 2026-07-09
브랜치: 2026-07-09_AutoArchitectureDocs
소스: /workflow 인수 텍스트

## 요구사항 요약
launchSettings.json·Program.cs·Hub 파일을 파싱하여 `Docs/architecture/overview.md`를
자동 재생성하는 Python 스크립트를 작성하고 `docs.yml`에 연결한다.
`sequences.md`는 현재 아키텍처(Game.Lobby SignalR 허브, Matching.API 내부 HTTP) 기준으로 수동 갱신한다.

## 상세 요구사항

1. **`.github/scripts/generate_architecture_docs.py` 신규 작성**
   - 각 서비스의 `Properties/launchSettings.json` → applicationUrl에서 포트 추출
   - 각 서비스의 `Program.cs` → `MapHub<T>`, `AddHttpClient`, `builder.Services.Add*` 파싱
   - Hub 파일 (LobbyHub.cs 등) → `IHttpClientFactory` 사용 패턴으로 서비스 간 HTTP 호출 방향 감지
   - 위 정보로 `Docs/architecture/overview.md` 동적 섹션을 재생성:
     - Mermaid 구성도 (서비스 노드·엣지)
     - 서비스별 책임 표 (포트, 주요 책임, Redis/DB 사용)
     - 통신 방식 표
     - 포트 맵 표
   - 정적 섹션 (핵심 설계 원칙, 서비스 경계 규칙, 프로젝트 의존성, 런타임 버전)은
     `<!-- STATIC_BEGIN -->` / `<!-- STATIC_END -->` 마커로 보호하여 덮어쓰지 않는다.
   - 스크립트는 로컬에서도 `python .github/scripts/generate_architecture_docs.py` 로 직접 실행 가능해야 한다.

2. **`docs.yml` 스텝 추가**
   - `generate_api_docs.py` 스텝 직후에 아래 스텝 삽입:
     ```yaml
     - name: Generate architecture docs
       run: python .github/scripts/generate_architecture_docs.py
     ```

3. **`overview.md` 1회 재생성 (스크립트 실행 결과 반영)**
   - 구버전 내용(Game.Server, Client→Matching 직접, MatchingHub SignalR) 제거
   - 신규 서비스 반영: Game.Lobby(SignalR :7777), Game.Gomoku(TCP :7778)
   - 매칭 흐름: Client → Lobby SignalR → Matching.API 내부 HTTP → Redis Pub/Sub → Lobby → Client

4. **`sequences.md` Section 3 수동 갱신**
   - "매칭 요청 → Game Server 접속" 시퀀스를 현재 흐름으로 재작성:
     - Client → Lobby SignalR `RequestMatch("gomoku")`
     - Lobby → Matching.API `POST /api/gamematch/request` (내부 HTTP)
     - Matching.API → Redis (매칭 처리)
     - Matching.API → Redis Pub/Sub `MATCH_FOUND_CHANNEL`
     - Game.Lobby MatchNotificationService → Client SignalR `MatchFound`
     - Client → Game.Gomoku TCP :7778

## 영향 범위 (예상)

| 파일 | 변경 종류 |
|------|---------|
| `.github/scripts/generate_architecture_docs.py` | 신규 생성 |
| `.github/workflows/docs.yml` | 스텝 1개 추가 |
| `Docs/architecture/overview.md` | 전체 재작성 (정적 섹션은 마커로 유지) |
| `Docs/architecture/sequences.md` | Section 3만 수정 |

C# 코드 변경 없음.

## 제약 및 주의사항

- `overview.md` 정적 섹션(핵심 설계 원칙, 서비스 경계 규칙, 프로젝트 의존성)은
  스크립트가 덮어쓰지 않도록 `<!-- STATIC_BEGIN -->` / `<!-- STATIC_END -->` 마커로 감싼다.
- 스크립트가 실패해도 docs.yml이 전체 실패하지 않도록 `|| true` 처리하지 말고
  정상 동작을 보장해야 한다 (실패 시 빌드 차단이 올바른 동작).
- launchSettings.json이 없는 서비스(Game.Gomoku TCP, Game.Lobby)는 포트를
  `appsettings.json` 또는 `Program.cs` Kestrel 설정에서 파싱하거나,
  스크립트 내 폴백 매핑 테이블에서 가져온다.
- GitHub Actions 워크플로에 DB 접근 코드 추가 금지 (CLAUDE.md 원칙 준수).

## 구현 접근 방향

1. 스크립트 구조:
   ```python
   parse_ports()        → {서비스명: 포트} dict
   parse_hubs()         → {서비스명: [허브경로]} dict
   parse_http_clients() → [(from_서비스, to_서비스, named_client)] list
   build_mermaid()      → mermaid 다이어그램 문자열
   build_service_table()→ 마크다운 표 문자열
   render_overview()    → 전체 overview.md 문자열 (정적 섹션은 기존 파일에서 읽어 삽입)
   ```

2. 정적 섹션 보호 방식:
   - 기존 `overview.md`에서 `<!-- STATIC_BEGIN -->` ~ `<!-- STATIC_END -->` 블록을 읽어
     신규 생성 파일에 그대로 삽입

3. sequences.md는 스크립트 대상이 아님 — 수동으로 Section 3만 교체

## 검증 기준

- `python .github/scripts/generate_architecture_docs.py` 로컬 실행 시 오류 없이 완료
- 생성된 `overview.md`에 Game.Lobby, Game.Gomoku 노드가 Mermaid 다이어그램에 포함
- 생성된 `overview.md`에 Client→Matching 직접 엣지가 없고 Client→Lobby→Matching 흐름 반영
- `sequences.md` Section 3가 현재 매칭 흐름(Lobby SignalR → Matching 내부 HTTP)으로 대체
- `docs.yml`에 `generate_architecture_docs.py` 스텝이 추가됨
- GitHub Actions `docs.yml` push 후 성공 (CI 통과)
