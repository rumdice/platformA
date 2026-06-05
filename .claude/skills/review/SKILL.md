---
name: review
schema_version: 1
description: PlatformA 프로젝트 코드 리뷰. PR 번호 또는 현재 브랜치 변경사항을 PlatformA 코딩 패턴, 아키텍처 원칙, Definition of Done 기준으로 검토한다.
allowed-tools: Bash(git *) Bash(grep *) Read Edit
---

# PlatformA 코드 리뷰

## 현재 변경사항
- 브랜치: !`git branch --show-current`
- 변경 파일: !`git diff --name-only HEAD~1 2>/dev/null || git diff --name-only --cached`

인자가 있으면 PR 번호로, 없으면 현재 브랜치의 변경사항을 리뷰한다: $ARGUMENTS

---

## 리뷰 체크리스트

### 1. 패킷 추가 (Game Server)
ADR-007: Protobuf 기준:
- [ ] `packets.proto`에 message 정의 + `Packet.oneof` 필드 등록이 되었는가?
- [ ] `PacketHandler.cs`에 `[PacketHandler]` 어트리뷰트로 핸들러가 등록되었는가?
- [ ] 핸들러가 반드시 `room.Push()` 안에서 게임 상태를 수정하는가?
- [ ] `BitConverter`, `BinaryReader`, `BinaryWriter` 등 수동 직렬화 코드가 없는가?
- [ ] proto3 기본값 주의: 0인 enum/int 필드는 wire에 포함되지 않음을 인지했는가?

### 2. API 엔드포인트 추가
- [ ] 컨트롤러 변경 후 `/doc-writer api-guide` 실행으로 `Docs/api-guide/` 동기화되었는가?
- [ ] DI는 생성자 주입만 사용하는가? (`new` 직접 생성 금지)
- [ ] JWT 검증 로직이 누락되지 않았는가? (인증 필요 엔드포인트)
- [ ] 에러 응답이 `{ Message = "..." }` 포맷을 따르는가?
- [ ] Rate Limit 필요 시 `[RedisRateLimit]` 어트리뷰트 적용되었는가?

### 3. DB 변경
- [ ] EF Core Migration이 생성되었는가? (직접 SQL 금지)
- [ ] 테이블명이 snake_case인가?
- [ ] TTL 없는 Redis 키는 없는가?
- [ ] `IDbContextFactory` 방식으로 DbContext를 DI받는가?

### 4. Redis 사용
- [ ] 새 Redis 키가 `PlatformA.Library/Common/Consts.cs`에 상수로 등록되었는가?
- [ ] 하드코딩된 키 문자열 없는가?
- [ ] `RedisManager.Instance.ExecuteAsync()` 래핑 사용하는가?
- [ ] 분산 락 사용 시 `finally`에서 반드시 릴리즈하는가?

### 5. 설정값
- [ ] 모든 상수/설정이 `Consts.cs`에만 있는가?
- [ ] `appsettings.json`에 로그 레벨 외 설정이 추가되지 않았는가?

### 6. 서비스 경계
`Docs/architecture/overview.md` 설계 원칙 기준:
- [ ] 서비스 간 직접 HTTP 호출이 없는가? (Redis Pub/Sub 우선)
- [ ] Game Server가 MySQL에 직접 접근하지 않는가?
- [ ] 각 서비스가 자신의 책임 범위를 벗어나지 않는가?

### 7. Definition of Done
- [ ] `dotnet build PlatformA.sln` 빌드 오류 없음
- [ ] `dotnet test` 전체 통과
- [ ] 관련 API 변경 시 `/doc-writer api-guide` 실행
- [ ] `AI/SPRINT.md` 해당 항목 체크

---

위 체크리스트를 기준으로 변경사항을 검토하고, 각 항목에 대해 **통과 / 위반 / 해당없음**으로 결과를 보고한다.
위반 항목은 파일 경로와 라인 번호를 포함하여 구체적으로 설명한다.

---

## 완료 처리

리뷰 결과 보고 후 task JSON에 리뷰 완료를 기록한다:

```bash
CURRENT_BRANCH=$(git branch --show-current)
TASK_FILE=$(grep -rl "\"branch\": \"${CURRENT_BRANCH}\"" AI/tasks/ 2>/dev/null | head -1)
```

TASK_FILE이 있으면 Edit 도구로 아래 두 가지를 갱신한다:

1. `"review_completed": false` → `"review_completed": true`

2. `steps[]` 배열에 아래 항목을 추가한다:
```json
{
  "name": "review",
  "status": "done",
  "completed_at": "{ISO8601 현재 시각}",
  "summary": "{종합 판정 — 승인/조건부 승인/반려}: {주요 발견 사항 1줄}"
}
```

없으면 이 단계를 건너뛴다.

TASK_FILE이 있으면 PostgreSQL dual-write 시도 (선택 — 연결 실패 시 무시):
```bash
python .github/scripts/db_write.py \
  --action insert-step \
  --branch "${CURRENT_BRANCH}" \
  --step-name "review" \
  --step-status "done" \
  --step-summary "리뷰 완료" 2>/dev/null || true
```
