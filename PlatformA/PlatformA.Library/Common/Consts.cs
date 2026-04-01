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
        public const string ACTIVE_KEY = "ticket:active:users"; // 입장 허용된 유저들이 모일 곳

        public const int WAIT_QUEUE_MAX_SIZE = 10000; // 대기열 최대 사이즈 (실무에서는 이 값도 DB 부하량에 따라 동적으로 바뀌어야 합니다.)
    }
}
