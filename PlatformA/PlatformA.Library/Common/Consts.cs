using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlatformA.Library.Common
{
    public static class Consts
    {
        public static readonly string SECRET_KEY =
            Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? "YourSuperSecretKeyForPlatformAMSA!@#123";

        // JWT Issuer/Audience: 타 서비스에서 발급한 토큰을 이 서버가 수락하지 않도록 검증합니다.
        public const string JWT_ISSUER = "PlatformA.Auth.API";
        public const string JWT_AUDIENCE = "PlatformA.Services";

        // Access Token: 짧은 만료로 탈취 피해 최소화
        // Refresh Token: 장기 유지, Redis에서 서버 측 관리 (강제 무효화 가능)
        public const int ACCESS_TOKEN_EXPIRY_MINUTES = 15;
        public const int REFRESH_TOKEN_EXPIRY_DAYS = 7;
        public const string REFRESH_TOKEN_KEY_PREFIX = "refresh:";

        // Redis Cluster 환경에서 멀티키 Lua 스크립트(LeaveQueue, GhostCleanup)가 동일 슬롯에
        // 위치하도록 해시태그 {ticket:queue} 를 사용합니다.
        // CRC16("{ticket:queue}") → 동일 슬롯 보장.
        public const string QUEUE_KEY = "{ticket:queue}:global";
        public const string QUEUE_HEARTBEATS_KEY = "{ticket:queue}:heartbeats";

        // Active 유저를 개별 키로 관리 (TTL 자동 만료 지원)
        // 사용법: $"{ACTIVE_USER_KEY_PREFIX}{userId}"
        public const string ACTIVE_USER_KEY_PREFIX = "ticket:active:user:";

        public const int WAIT_QUEUE_MAX_SIZE = 10000; // 대기열 최대 사이즈 (실무에서는 이 값도 DB 부하량에 따라 동적으로 바뀌어야 합니다.)

        // 대기열 처리 속도: 환경 변수로 재배포 없이 조정 가능
        // QUEUE_BASE_RATE: 평시 기준 속도 (기본 50명/초)
        // QUEUE_MAX_RATE: 게임 서버가 견딜 수 있는 최대 속도 (기본 500명/초)
        public static readonly int QUEUE_BASE_RATE =
            int.TryParse(Environment.GetEnvironmentVariable("QUEUE_BASE_RATE"), out var qbr) ? qbr : 50;

        public static readonly int QUEUE_MAX_RATE =
            int.TryParse(Environment.GetEnvironmentVariable("QUEUE_MAX_RATE"), out var qmr) ? qmr : 500;

        // Active 유저 입장권 만료 시간: 이 시간 안에 게임 서버에 접속하지 않으면 입장권이 소멸됩니다.
        public const int ACTIVE_USER_TTL_SECONDS = 300; // 5분

        // 접속 정보: 환경변수로 주입 (로컬 기본값 유지, K8s/Docker는 ConfigMap/env로 오버라이드)
        public static readonly string GAME_SERVER_IP =
            Environment.GetEnvironmentVariable("GAME_SERVER_IP") ?? "127.0.0.1";

        public static readonly int GAME_SERVER_PORT =
            int.TryParse(Environment.GetEnvironmentVariable("GAME_SERVER_PORT"), out int gsp) ? gsp : 7777;

        // Redis Cluster 노드 목록 (Master 3개 — StackExchange.Redis가 Slave를 자동 감지)
        public static readonly string REDIS_CONNECTION_STRING =
            Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
            ?? "127.0.0.1:6371,127.0.0.1:6372,127.0.0.1:6373";


        public static readonly string MYSQL_WEBAPP_CONNECTION =
            Environment.GetEnvironmentVariable("MYSQL_WEBAPP_CONNECTION_STRING")
            ?? "Server=localhost;Port=3306;Database=db_WebApp;User=root;Password=pass1234";

        public static readonly string MYSQL_LOGAPP_CONNECTION =
            Environment.GetEnvironmentVariable("MYSQL_LOGAPP_CONNECTION_STRING")
            ?? "Server=localhost;Port=3306;Database=db_LogApp;User=root;Password=pass1234";

        public static readonly string AUTH_API_URL =
            Environment.GetEnvironmentVariable("AUTH_API_URL")
            ?? "https://localhost:7001/api/Auth/login";

        public static readonly string AUTH_API_REFRESH_URL =
            Environment.GetEnvironmentVariable("AUTH_API_REFRESH_URL")
            ?? "https://localhost:7001/api/Auth/refresh";

        public static readonly string TICKET_API_URL =
            Environment.GetEnvironmentVariable("TICKET_API_URL")
            ?? "https://localhost:7003";

        public static readonly string MATCH_API_URL =
            Environment.GetEnvironmentVariable("MATCH_API_URL")
            ?? "https://localhost:7002/api/GameMatch/RequestMatch";

        public static readonly string MATCH_HUB_URL =
            Environment.GetEnvironmentVariable("MATCH_HUB_URL")
            ?? "https://localhost:7002/hubs/matching";

        // Redis Sorted Set 기반 매칭 대기열 (score = 입장 시각 UnixMs, 타임아웃 추적 가능)
        public const string MATCH_QUEUE_KEY = "queue:gamematch:1v1";
        public const int MATCH_TIMEOUT_SECONDS = 120; // 2분 초과 시 MatchTimeout 이벤트 push

    }
}
