---
name: doc-writer
description: 소스 코드를 분석하여 Docs/ 문서를 자동 생성·업데이트한다. api-guide, operations, developer-guide 섹션을 지원하며 생성 후 로컬 빌드·서빙까지 수행한다.
allowed-tools: Agent Bash(dotnet *) Bash(ls *) Bash(mkdir *) Read Write Edit Glob Grep
---

# /doc-writer 문서 자동 생성 스킬

## 인수
```
/doc-writer [섹션]
```
- `api-guide` — 컨트롤러 소스에서 REST API 명세 생성
- `operations` — K8s/Docker 파일에서 배포 가이드 생성
- `developer-guide` — PATTERNS.md, 패킷 프로토콜 등 개발 가이드 생성
- `all` (기본값) — 위 세 가지 모두 수행

인수가 없으면 `all`로 동작한다.

---

## 수행 절차

### 0단계 — 대상 파일 결정

인수에 따라 읽을 소스 파일과 작성할 문서 파일을 결정한다.

| 섹션 | 소스 파일 | 생성 문서 |
|------|----------|----------|
| api-guide | `PlatformA.Auth.API/Controllers/AuthController.cs`<br>`PlatformA.Ticketing.API/Controllers/QueueController.cs`<br>`PlatformA.Matching.API/Controllers/GameMatchController.cs`<br>`PlatformA.Matching.API/Controllers/OrderController.cs`<br>`PlatformA.Utils.API/Controllers/UtilController.cs` | `Docs/api-guide/auth.md`<br>`Docs/api-guide/ticketing.md`<br>`Docs/api-guide/matching.md`<br>`Docs/api-guide/utils.md` |
| operations | `k8s/` 매니페스트 전체<br>`PlatformA/docker/` 전체<br>`AI/RUNBOOK.md` | `Docs/operations/deployment.md`<br>`Docs/operations/monitoring.md` |
| developer-guide | `.claude/rules/patterns.md`<br>`PlatformA.Library/Packets/Proto/packets.proto`<br>`AI/ARCHITECTURE.md`<br>`PlatformA.Library/Common/Consts.cs` | `Docs/developer-guide/coding-patterns.md`<br>`Docs/developer-guide/packet-protocol.md`<br>`Docs/developer-guide/redis-patterns.md` |

### 1단계 — Agent로 문서 생성

`general-purpose` 에이전트를 섹션별로 실행한다.

에이전트에 전달하는 프롬프트 구조:
```
다음 소스 파일들을 읽고 [섹션명] 한국어 문서를 작성한다.

읽을 파일: [소스 목록]
생성할 파일: [문서 경로 목록]

문서 형식 규칙:
- 각 엔드포인트: HTTP 메서드 + URL + 설명 + 요청/응답 표 + 오류 코드 표
- 존재하지 않는 기능 추가 금지
- Mermaid 다이어그램 포함 (흐름이 있는 경우)
- 한국어 작성, 코드 블록 영어 유지

Write 도구로 직접 파일을 생성한다.
```

### 2단계 — toc.yml 갱신

생성된 문서 파일 목록을 기반으로 해당 섹션의 `toc.yml`을 생성 또는 업데이트한다.

```yaml
# api-guide/toc.yml 예시
- name: Auth API
  href: auth.md
- name: Ticketing API
  href: ticketing.md
- name: Matching API
  href: matching.md
- name: Utils API
  href: utils.md
```

### 3단계 — 로컬 빌드

```powershell
$env:PATH = "$env:PATH;$env:USERPROFILE\.dotnet\tools"
Remove-Item -Recurse -Force Docs/_site -ErrorAction SilentlyContinue
docfx Docs/docfx.json
```

빌드 결과 확인:
- `Build succeeded` 메시지 확인
- `0 error(s)` 확인
- 오류 시 즉시 중단하고 오류 내용 보고

### 4단계 — 로컬 서버 시작

```powershell
# 기존 8080 프로세스 정리
$conn = Get-NetTCPConnection -LocalPort 8080 -ErrorAction SilentlyContinue | Select-Object -First 1
if ($conn) { Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue }

# 서버 시작
& "$env:USERPROFILE\.dotnet\tools\dotnet-serve.exe" `
    --directory Docs/_site `
    --port 8080 `
    --open-browser
```

### 5단계 — 결과 보고

다음 형식으로 요약 보고:

```
## /doc-writer 완료 보고

### 생성된 문서
- ✅ Docs/api-guide/auth.md
- ✅ Docs/api-guide/ticketing.md
...

### 빌드 결과
- 오류: 0개
- 경고: N개

### 로컬 확인
http://localhost:8080 에서 확인 가능

### 서버 종료 방법
PowerShell에서 실행:
$conn = Get-NetTCPConnection -LocalPort 8080 -ErrorAction SilentlyContinue | Select-Object -First 1
if ($conn) { Stop-Process -Id $conn.OwningProcess -Force }
```

---

## 서버 관리 명령

### 서버 종료
```powershell
$conn = Get-NetTCPConnection -LocalPort 8080 -ErrorAction SilentlyContinue | Select-Object -First 1
if ($conn) { Stop-Process -Id $conn.OwningProcess -Force; Write-Host "8080 종료됨" }
```

### 서버 재시작
```powershell
# 종료
$conn = Get-NetTCPConnection -LocalPort 8080 -ErrorAction SilentlyContinue | Select-Object -First 1
if ($conn) { Stop-Process -Id $conn.OwningProcess -Force }
# 시작
& "$env:USERPROFILE\.dotnet\tools\dotnet-serve.exe" --directory Docs/_site --port 8080 --open-browser
```

---

## 문서 품질 기준

생성된 문서는 다음 기준을 충족해야 한다:

- [ ] 소스 코드에 실제로 존재하는 엔드포인트만 기술
- [ ] 요청 Body / 쿼리 파라미터 표 포함
- [ ] 응답 예시 JSON 포함
- [ ] HTTP 오류 코드 표 포함
- [ ] 로컬 빌드에서 `InvalidFileLink` 경고 없음

---

## 자동화 연계 계획

`/done` 워크플로우에서 `.cs` 파일 변경이 감지되면 `/doc-writer api-guide`를 자동 호출하여  
코드 변경과 문서를 동기화할 수 있다. (향후 `/done` 스킬 업데이트 시 연계)
