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

        // Active 유저 입장권 만료 시간: 이 시간 안에 게임 서버에 접속하지 않으면 입장권이 소멸됩니다.
        public const int ACTIVE_USER_TTL_SECONDS = 300; // 5분

        // TODO: 접속정보들은 차후 config 파일 또는 aws SKS로 관리하도록 개선 필요
        public const string GAME_SERVER_IP = "127.0.0.1";
        public const int GAME_SERVER_PORT = 7777;

        // Redis Cluster 노드 목록 (Master 3개 — StackExchange.Redis가 Slave를 자동 감지)
        public const string REDIS_CONNECTION_STRING = "127.0.0.1:6371,127.0.0.1:6372,127.0.0.1:6373";


        public static readonly string MYSQL_WEBAPP_CONNECTION =
            Environment.GetEnvironmentVariable("MYSQL_WEBAPP_CONNECTION_STRING")
            ?? "Server=localhost;Port=3306;Database=db_WebApp;User=root;Password=pass1234";

        public static readonly string MYSQL_LOGAPP_CONNECTION =
            Environment.GetEnvironmentVariable("MYSQL_LOGAPP_CONNECTION_STRING")
            ?? "Server=localhost;Port=3306;Database=db_LogApp;User=root;Password=pass1234";

        public const string AUTH_API_URL = "https://localhost:7088/api/Auth/login";
        public const string AUTH_API_REFRESH_URL = "https://localhost:7088/api/Auth/refresh";

        public const string TICKET_API_URL = "https://localhost:7075";

        public const string MATCH_API_URL = "http://localhost:5189/api/GameMatch/RequestMatch";
        public const string MATCH_HUB_URL = "http://localhost:5189/hubs/matching";

    }
}
