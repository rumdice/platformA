# Plan: Ticketing.API 코드 정리

## Context
Ticketing.API에 실제 운영 코드(QueueController, QueueService, QueueHub)와
초기 학습용 데모 코드(TicketController — 아이유 콘서트 티켓 예제)가 혼재해 있다.
데모 코드는 의도적 Race Condition, 미완성 TODO, 항상 403을 반환하는 dead code를 포함한다.
코드 정리로 운영 코드만 남기고 가독성·안전성을 높인다.

---

## 변경 파일

| 파일 | 작업 |
|---|---|
| `Controllers/TicketController.cs` | **삭제** — 전체가 데모/학습용 코드 |
| `Controllers/QueueController.cs` | 주석 제거, 예외 노출 수정, StartsWith 오타 수정 |
| `Hubs/QueueHub.cs` | 빈 `OnDisconnectedAsync` 제거 |
| `Program.cs` | TicketController 삭제에 따른 DI 등록 제거 확인 |

---

## 세부 변경 내용

### 1. TicketController.cs 삭제
- 모든 메서드가 데모 목적: BuyTicketBad(Race Condition 시연), BuyTicketGood/Manual(락 비교), BuyTicketFinal(항상 403 반환)
- "ticket:iu_concert" 하드코딩 키, 의도적 지연(Task.Delay(10)), 미완성 TODO 다수
- QueueController가 실제 운영 대기열을 담당 — TicketController는 완전히 분리된 데모

### 2. QueueController.cs 정리

**제거: 주석 처리된 코드 블록 (lines 63–72)**
```csharp
// 삭제 대상
//// 폴링 중인 유저의 하트비트 갱신 — Ghost 오탐 방지
//await _queueService.UpdateHeartbeatAsync(userId);
//// GetRank
//long? rank = await _queueService.GetRankAsync(userId);
//if (rank.HasValue) { ... }
```

**수정: 예외 메시지 직접 노출 → 내부 로깅 후 일반 메시지 반환 (lines 46, 108)**
```csharp
// Before
return BadRequest(ex.Message);

// After
return BadRequest(new { Message = "요청 처리 중 오류가 발생했습니다." });
// (_logger.LogError 는 이미 윗줄에 있으므로 로그는 유지)
```

**수정: StartsWith("Bearer") → StartsWith("Bearer ") (line 115)**
```csharp
// Before
if (... !authHeader.StartsWith("Bearer"))

// After
if (... !authHeader.StartsWith("Bearer "))
// QueueHub.cs는 이미 "Bearer "(공백 포함)로 올바르게 작성됨 — 통일
```

### 3. QueueHub.cs 정리

**제거: 빈 OnDisconnectedAsync (lines 49–52)**
```csharp
// 삭제 대상 — base 호출만 하는 override는 없는 것과 동일
public override async Task OnDisconnectedAsync(Exception? exception)
{
    await base.OnDisconnectedAsync(exception);
}
```

### 4. Program.cs 확인
TicketController 삭제 후 아래 DI 등록이 불필요해질 수 있음:
- `RedLockFactory` 등록
- `RedisLockManager` 등록
- `QueueService`는 QueueController에서도 사용 — 유지

---

## 검증
```bash
cd PlatformA && dotnet build PlatformA.sln -q   # 빌드 오류 0개
dotnet test PlatformA.sln -q                     # 테스트 전체 통과
```
TicketController 관련 테스트가 없으므로 테스트 영향 없음.
빌드 후 Program.cs에서 미사용 서비스 등록 경고 확인.
