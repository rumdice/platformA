# ADR-001: Redis Cluster (3 Master + 3 Replica) 선택

## 상태: 확정

## 날짜: 2026-04-21

---

## 맥락

다음 데이터를 고가용성으로 저장/처리해야 함:
- 대기열 ZSet (최대 10,000명)
- Refresh Token (동시 다중 세션)
- Rate Limiting (슬라이딩 윈도우, IP 단위)
- 분산 락 (중복 로그인 방지)
- Pub/Sub 채널 (매칭 성공 이벤트)

단일 Redis 인스턴스는 SPOF(단일 장애점)이며, 서비스 중단 시 대기열 데이터 전체 소실.

---

## 결정

**Redis Cluster 사용 (3 Master + 3 Replica)**

- Master 3개: 16384 슬롯을 균등 분배 (슬롯당 1/3씩)
- Replica 3개: 각 Master의 복제본 (자동 페일오버)
- 포트: 6371~6376 (버스 포트: 16371~16376)
- 연결: `StackExchange.Redis` (Cluster 모드 자동 인식)
- 복원력: Polly (재시도 3회 + 서킷 브레이커 60초)

---

## 대안과 기각 이유

| 대안 | 기각 이유 |
|------|---------|
| Redis Sentinel | 단일 Master 쓰기 병목. 10,000명 대기열 처리 불가 |
| Redis Standalone | SPOF. 재시작 시 전체 대기열 소실 |
| In-Memory (ConcurrentQueue) | 서버 재시작 시 데이터 소실. 수평 확장 불가 |
| Hazelcast | .NET 생태계 지원 미흡, 운영 복잡도 높음 |

---

## 결과 및 트레이드오프

**이득:**
- Master 장애 시 Replica 자동 승격 (수초 내)
- 읽기 부하를 Replica로 분산 가능
- 슬롯 기반 수평 확장 (노드 추가만으로 확장)

**비용:**
- `{ticket:queue}:global` 처럼 해시 태그 `{}` 필수 (같은 슬롯에 배치)
- MULTI/EXEC 트랜잭션은 단일 슬롯 내에서만 가능
- 로컬 개발 환경 설정 복잡도 증가

---

## 변경 방법

이 결정을 변경하려면:
1. 새 ADR 작성 (`AI/adr/NNN-제목.md`)
2. 사용자 승인 후 진행
3. 기존 ADR 상태를 "대체됨 (→ ADR-NNN)"으로 업데이트
