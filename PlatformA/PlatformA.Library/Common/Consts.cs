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

    }
}
