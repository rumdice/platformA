using PlatformA.Library.Common;
using PlatformA.Library.Core;
using StackExchange.Redis;

namespace PlatformA.Ticketing.API.Workers
{
    public class QueueWorkerService : BackgroundService
    {
        private readonly RedisManager _redisManager;

        // 초당 입장 인원 (실무에서는 DB 부하량에 따라 동적으로 조절)
        private const int USERS_PER_SECOND = 50;

        // 분산 락 설정
        private const string WORKER_LEADER_LOCK = "lock:queue:worker:leader";

        // 락 TTL: ProcessQueueAsync 처리 중 만료되지 않을 만큼 충분히 크게 설정
        private static readonly TimeSpan LOCK_EXPIRY = TimeSpan.FromSeconds(10);

        // 갱신 주기: TTL의 1/3 수준으로 설정해 네트워크 지연 발생 시에도 3번의 기회 확보
        private static readonly TimeSpan LOCK_RENEW_INTERVAL = TimeSpan.FromSeconds(3);

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
                    // 분산 락: 리더 인스턴스만 큐 처리 수행
                    string? leaderLock = await _redisManager.LockManager.AcquireLockAsync(
                        WORKER_LEADER_LOCK,
                        expiry: LOCK_EXPIRY,
                        waitTime: TimeSpan.FromMilliseconds(50),
                        retryTime: TimeSpan.FromMilliseconds(10)
                    );

                    if (leaderLock == null)
                    {
                        await Task.Delay(1000, stoppingToken);
                        continue;
                    }

                    // [Lock Heartbeat]
                    // ProcessQueueAsync 실행 중 락이 만료되지 않도록
                    // 별도 태스크가 LOCK_RENEW_INTERVAL 마다 TTL을 갱신합니다.
                    using var renewCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

                    var renewTask = RenewLockPeriodicallyAsync(leaderLock, renewCts.Token);

                    try
                    {
                        await ProcessQueueAsync(db);
                    }
                    finally
                    {
                        // 처리 완료 → 갱신 태스크 중단 후 락 즉시 해제
                        await renewCts.CancelAsync();
                        await renewTask;
                        await _redisManager.LockManager.ReleaseLockAsync(WORKER_LEADER_LOCK, leaderLock);
                    }

                    await Task.Delay(1000, stoppingToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[QueueWorker] 에러 발생: {ex.Message}");
                    await Task.Delay(2000, stoppingToken);
                }
            }
        }

        /// <summary>
        /// 락 갱신 태스크. 취소될 때까지 LOCK_RENEW_INTERVAL 주기로 TTL을 연장합니다.
        /// 갱신 실패(락을 잃었을 경우)는 경고 로그만 남기고 종료합니다.
        /// </summary>
        private async Task RenewLockPeriodicallyAsync(string lockValue, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(LOCK_RENEW_INTERVAL, ct);

                    bool renewed = await _redisManager.LockManager.RenewLockAsync(
                        WORKER_LEADER_LOCK, lockValue, LOCK_EXPIRY
                    );

                    if (!renewed)
                    {
                        // 락 TTL이 이미 만료되어 타 인스턴스에 넘어간 상태
                        Console.WriteLine("[QueueWorker] ⚠️ 락 갱신 실패 — 처리 시간이 LOCK_EXPIRY를 초과했습니다.");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 정상 취소 — 조용히 종료
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

                // Active 상태를 개별 키 + TTL로 저장 (TTL 만료 시 자동 정리)
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
