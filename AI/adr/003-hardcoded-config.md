# ADR-003: 설정값 Consts.cs 중앙화 (기술 부채)

## 상태: 확정 (개선 필요)

## 날짜: 2026-04-21

---

## 맥락

현재 모든 연결 문자열, 포트, JWT 시크릿, Redis 주소가 `PlatformA.Library/Common/Consts.cs`에 하드코딩되어 있음.

```csharp
// 현재 상태 (Consts.cs)
public const string SECRET_KEY = "YourSuperSecretKeyForPlatformAMSA!@#123";
public const string MYSQL_WEBAPP_CONNECTION = "Server=localhost;...Password=pass1234";
public const string REDIS_CONNECTION_STRING = "127.0.0.1:6371,...";
```

코드 내 주석: `// TODO: 접속정보들은 차후 config 파일 또는 AWS SKS로 관리하도록 개선 필요`

---

## 결정 (현재)

**개발 단계에서는 Consts.cs 단일 파일에서 모든 설정 관리**

이유:
- 멀티 프로젝트 솔루션에서 설정 변경 시 한 곳만 수정
- 환경변수 인프라 없이 로컬 개발 편의성 최대화
- 초기 프로토타입 단계에서 속도 우선

---

## 개선 방향 (미적용 상태)

프로덕션 배포 전 아래 순서로 개선 필요:

1. **1단계**: `appsettings.json` + `appsettings.Production.json` 분리
   - 민감하지 않은 설정 (포트, TTL 등)만 이동
   
2. **2단계**: 환경변수로 민감 정보 분리
   - `MYSQL_PASSWORD`, `REDIS_PASSWORD`, `JWT_SECRET` 등
   - `dotnet user-secrets` (개발), Docker env (스테이징)

3. **3단계 (선택)**: AWS Secrets Manager / Parameter Store
   - 프로덕션 환경에서만

---

## 현재 규칙

- 설정값 추가 시 반드시 `Consts.cs`에만 추가
- `appsettings.json`은 로그 레벨만 관리 (현재 상태 유지)
- 각 API의 `Program.cs`는 `Consts.XXX`로만 설정 참조

---

## 보안 주의사항

- JWT_SECRET, DB 비밀번호가 소스코드에 노출됨
- 개발/테스트 환경 전용으로만 사용할 것
- 프로덕션 배포 전 반드시 환경변수로 이전

---

## 변경 방법

1단계 이상 적용 시 새 ADR 작성 + 사용자 승인 필요.
`Consts.cs` 완전 제거는 전체 API 수정 필요 (범위 큼).
