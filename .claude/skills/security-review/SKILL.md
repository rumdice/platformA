---
name: security-review
description: PlatformA 게임 백엔드 보안 검토. JWT 인증, Redis 분산 락, Rate Limiting, SQL Injection, 입력 검증 등 게임 서버 특화 보안 항목을 점검한다.
---

# PlatformA 보안 리뷰

## 현재 변경사항
- 브랜치: !`git branch --show-current`
- 변경 파일: !`git diff --name-only HEAD~1 2>/dev/null || git diff --name-only --cached`

인자가 있으면 해당 범위로, 없으면 현재 브랜치 전체 변경사항을 검토한다: $ARGUMENTS

---

## 보안 체크리스트

### 1. JWT 인증
- [ ] 모든 인증 필요 엔드포인트에서 `Authorization` 헤더를 검증하는가?
- [ ] `TokenManager.ValidateTokenAndGetUserId()` 반환값이 `<= 0`일 때 `Unauthorized` 반환하는가?
- [ ] JWT 시크릿이 코드/설정 파일에 하드코딩되지 않았는가?
- [ ] Access Token 만료 시간이 적절한가? (현재 기준: 15분)

### 2. Redis 보안
- [ ] 분산 락 없이 동시 접근 가능한 임계 구역이 없는가?
- [ ] 락 릴리즈가 반드시 `finally` 블록에서 이루어지는가?
- [ ] `BrokenCircuitException` 처리가 누락되지 않았는가? (서킷 오픈 시 인증 차단)
- [ ] 새 Redis 키에 TTL이 설정되어 있는가? (TTL 없는 키 원칙 금지)

### 3. Rate Limiting
- [ ] 외부 노출 엔드포인트에 `[RedisRateLimit]` 어트리뷰트가 적용되었는가?
- [ ] Rate Limit 정책명이 올바르게 지정되었는가?
- [ ] Rate Limit 임계값이 서비스 용도에 적합한가? (Auth: 10req/s, Ticketing: 5req/s)

### 4. 입력 검증
- [ ] Request DTO에 DataAnnotation(`[Required]`, `[MinLength]`, `[MaxLength]`, `[Range]`)이 적용되었는가?
- [ ] 문자열 입력에 길이 제한이 있는가?
- [ ] 정규식 검증이 필요한 필드(username 등)에 `[RegularExpression]`이 있는가?

### 5. SQL / EF Core
- [ ] Raw SQL 실행 (`ExecuteSqlRaw`, `FromSqlRaw`)이 없는가? (EF Core LINQ 사용 필수)
- [ ] 사용자 입력이 LINQ 쿼리에 직접 문자열 포맷으로 들어가지 않는가?
- [ ] Migration 없이 스키마를 변경하는 코드가 없는가?

### 6. 패킷 보안 (Game Server)
- [ ] 패킷 크기 검증이 누락되지 않았는가?
- [ ] `room.Push()` 외부에서 게임 상태를 수정하는 코드가 없는가? (레이스 컨디션)
- [ ] 클라이언트가 보낸 `SenderId`를 그대로 신뢰하지 않는가? (서버 측 세션에서 추출해야 함)

### 7. 민감 정보 로깅
- [ ] 비밀번호, 토큰, 개인정보가 로그에 출력되지 않는가?
- [ ] 예외 로그에 스택 트레이스가 포함되어 있는가? (`_logger.LogError(ex, ...)`)
- [ ] 성공 로그가 과도하게 민감한 정보를 포함하지 않는가?

### 8. BCrypt / 비밀번호
- [ ] 비밀번호 비교 시 `BCrypt.Verify()` 사용하는가? (평문 비교 절대 금지)
- [ ] 비밀번호 저장 시 `BCrypt.HashPassword()` 사용하는가?

---

위 체크리스트를 기준으로 검토하고 **통과 / 위반 / 해당없음**으로 결과를 보고한다.
위반 항목은 파일 경로, 라인 번호, 공격 시나리오를 포함하여 구체적으로 설명한다.
심각도는 **Critical / High / Medium / Low**로 분류한다.
