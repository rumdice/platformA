using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlatformA.Library.Common
{
    public static class Consts
    {
        public const string SECRET_KEY = "YourSuperSecretKeyForPlatformAMSA!@#123";
        
        public const string QUEUE_KEY = "ticket:queue:global";
        public const string ACTIVE_KEY = "ticket:active:users"; // @Deprecated: 개별 키 방식(ACTIVE_USER_KEY_PREFIX)으로 전환됨

        // Active 유저를 개별 키로 관리 (TTL 자동 만료 지원)
        // 사용법: $"{ACTIVE_USER_KEY_PREFIX}{userId}"
        public const string ACTIVE_USER_KEY_PREFIX = "ticket:active:user:";

        public const int WAIT_QUEUE_MAX_SIZE = 10000; // 대기열 최대 사이즈 (실무에서는 이 값도 DB 부하량에 따라 동적으로 바뀌어야 합니다.)

        // Active 유저 입장권 만료 시간: 이 시간 안에 게임 서버에 접속하지 않으면 입장권이 소멸됩니다.
        public const int ACTIVE_USER_TTL_SECONDS = 300; // 5분

        // TODO: 접속정보들은 차후 config 파일 또는 aws SKS로 관리하도록 개선 필요
        public const string GAME_SERVER_IP = "127.0.0.1";
        public const int GAME_SERVER_PORT = 7777;

        public const string REDIS_CONNECTION_STRING = "127.0.0.1:6379";

        public const string AUTH_API_URL = "https://localhost:7088/api/Auth/login";

        public const string TICKET_API_URL = "https://localhost:7075";
        
        public const string MATCH_API_URL = "http://localhost:5189/api/GameMatch/RequestMatch";
        public const string MATCH_HUB_URL = "http://localhost:5189/hubs/matching";

    }
}
