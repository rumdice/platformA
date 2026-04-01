using PlatformA.Library.Common;
using PlatformA.Library.Core;
using StackExchange.Redis;

namespace PlatformA.Ticketing.API.Workers
{
    public class QueueWorkerService : BackgroundService
    {
        private readonly RedisManager _redisManager;

        // 💡 다이나믹 스로틀링의 핵심: 1초에 몇 명을 통과시킬 것인가?
        // (실무에서는 이 값을 DB 부하량에 따라 동적으로 바뀌어야 합니다.)
        private const int USERS_PER_SECOND = 50;

        // 분산 락 키: 여러 인스턴스 중 하나만 Worker 역할을 수행하게 합니다.
        private const string WORKER_LEADER_LOCK = "lock:queue:worker:leader";

        public QueueWorkerService(RedisManager redisManager)
        {
            _redisManager = redisManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("티켓팅 문지기 워커 가동! (초당 입장 인원 제어 중...)");
            var db = _redisManager.Connection.GetDatabase();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // [핵심] 분산 락: 여러 Ticketing.API 인스턴스가 동시에 실행될 때
                    // 각 인스턴스가 독립적으로 ZPOPMIN을 실행하면 초당 N*50명이 통과됩니다.
                    // 락을 획득한 인스턴스만 실제 처리를 수행합니다.
                    string? leaderLock = await _redisManager.LockManager.AcquireLockAsync(
                        WORKER_LEADER_LOCK,
                        expiry: TimeSpan.FromSeconds(3),      // 락 자동 만료 (처리 중 크래시 대비)
                        waitTime: TimeSpan.FromMilliseconds(50),   // 리더가 아니면 빠르게 포기
                        retryTime: TimeSpan.FromMilliseconds(10)
                    );

                    if (leaderLock == null)
                    {
                        // 다른 인스턴스가 리더, 이번 주기는 스킵
                        await Task.Delay(1000, stoppingToken);
                        continue;
                    }

                    try
                    {
                        await ProcessQueueAsync(db);
                    }
                    finally
                    {
                        await _redisManager.LockManager.ReleaseLockAsync(WORKER_LEADER_LOCK, leaderLock);
                    }

                    // 🚀 이 딜레이가 없으면 초당 50명이 아니라 초당 수만 명이 통과됩니다!
                    await Task.Delay(1000, stoppingToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[QueueWorker] 에러 발생: {ex.Message}");
                    await Task.Delay(2000, stoppingToken);
                }
            }
        }

        private async Task ProcessQueueAsync(IDatabase db)
        {
            // 1. 유령 유저 청소 (60초 이상 통신이 끊긴 유저)
            double cutoffTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 60000;

            var ghostCleanupScript = @"
local ghosts = redis.call('ZRANGEBYSCORE', KEYS[1], '-inf', ARGV[1])
if #ghosts == 0 then return 0 end

for _, ghost in ipairs(ghosts) do
    redis.call('ZREM', KEYS[2], ghost)
end

redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', ARGV[1])
return #ghosts";

            var removed = (int)await db.ScriptEvaluateAsync(
                ghostCleanupScript,
                new RedisKey[] { "ticket:queue:heartbeats", Consts.QUEUE_KEY },
                new RedisValue[] { cutoffTime }
            );

            if (removed > 0)
                Console.WriteLine($"🧹 [청소 완료] 유령 유저 {removed}명 강제 퇴출!");

            // 2. 대기열(ZSET)에 사람이 있는지 확인
            long queueLength = await db.SortedSetLengthAsync(Consts.QUEUE_KEY);
            if (queueLength == 0) return;

            // ZPOPMIN: 줄의 맨 앞(가장 점수가 낮은)에서 N명을 뽑아냅니다.
            var poppedUsers = await db.SortedSetPopAsync(Consts.QUEUE_KEY, USERS_PER_SECOND);

            foreach (var user in poppedUsers)
            {
                int userId = (int)user.Element;

                // 3. Active 상태를 개별 키 + TTL로 저장
                // TTL이 지나면 자동으로 만료되어 Redis 메모리 누수를 방지합니다.
                await db.StringSetAsync(
                    $"{Consts.ACTIVE_USER_KEY_PREFIX}{userId}",
                    "1",
                    TimeSpan.FromSeconds(Consts.ACTIVE_USER_TTL_SECONDS)
                );

                Console.WriteLine($"[입장 허용 🟢] 유저 {userId}님이 대기열을 통과했습니다! (입장권 유효: {Consts.ACTIVE_USER_TTL_SECONDS}초)");
            }
        }
    }
}
