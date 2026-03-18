using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlatformA.Library.Core
{
    public class RedisManager
    {
        public static RedisManager Instance { get; } = new RedisManager();

        private ConnectionMultiplexer _redis;
        public RedisLockManager LockManager { get; private set; }

        private RedisManager() { }

        public void Init(string connectionString = "127.0.0.1:6379")
        {
            // TODO: local only
            // docker run --name my-redis -p 6379:6379 -d redis 로컬 환경에서 설치 필요.
            try
            {
                // Redis 서버 연결
                _redis = ConnectionMultiplexer.Connect(connectionString);

                // 개발자님이 만들어두신 RedisLockManager 연동
                LockManager = new RedisLockManager(_redis);

                Console.WriteLine($"[RedisManager] Redis 연결 및 LockManager 초기화 성공! ({connectionString})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RedisManager] Redis 연결 실패: {ex.Message}");
            }
        }
    }
}
