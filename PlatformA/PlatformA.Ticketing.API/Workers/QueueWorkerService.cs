using PlatformA.Library.Common;
using PlatformA.Library.Core;
using StackExchange.Redis;

namespace PlatformA.Ticketing.API.Workers
{
    public class QueueWorkerService : BackgroundService
    {
        private readonly RedisManager _redisManager;
        private readonly ILogger<QueueWorkerService> _logger;

        private const int USERS_PER_SECOND = 50;
        private const string WORKER_LEADER_LOCK = "lock:queue:worker:leader";
        private static readonly TimeSpan LOCK_EXPIRY        = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan LOCK_RENEW_INTERVAL = TimeSpan.FromSeconds(3);

        public QueueWorkerService(RedisManager redisManager, ILogger<QueueWorkerService> logger)
        {
            _redisManager = redisManager;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[QueueWorker] 티켓팅 문지기 워커 가동! (초당 입장: {Rate}명)", USERS_PER_SECOND);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
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

                    using var renewCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    var renewTask = RenewLockPeriodicallyAsync(leaderLock, renewCts.Token);

                    try
                    {
                        await ProcessQueueAsync();
                    }
                    finally
                    {
                        await renewCts.CancelAsync();
                        await renewTask;
                        await _redisManager.LockManager.ReleaseLockAsync(WORKER_LEADER_LOCK, leaderLock);
                    }

                    await Task.Delay(1000, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[QueueWorker] 처리 중 예외 발생");
                    await Task.Delay(2000, stoppingToken);
                }
            }
        }

        private async Task RenewLockPeriodicallyAsync(string lockValue, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(LOCK_RENEW_INTERVAL, ct);
                    bool renewed = await _redisManager.LockManager.RenewLockAsync(
                        WORKER_LEADER_LOCK, lockValue, LOCK_EXPIRY);
                    if (!renewed)
                    {
                        _logger.LogWarning("[QueueWorker] 락 갱신 실패 — LOCK_EXPIRY 초과");
                        break;
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task ProcessQueueAsync()
        {
            // 1. 유령 유저 청소 (60초 이상 통신 없는 유저)
            // QUEUE_KEY + QUEUE_HEARTBEATS_KEY : 동일 해시태그 {ticket:queue} → 같은 슬롯
            double cutoffTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 60_000;
            var ghostScript = @"
local ghosts = redis.call('ZRANGEBYSCORE', KEYS[1], '-inf', ARGV[1])
if #ghosts == 0 then return 0 end
for _, ghost in ipairs(ghosts) do
    redis.call('ZREM', KEYS[2], ghost)
end
redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', ARGV[1])
return #ghosts";

            var removed = (int)await _redisManager.ExecuteAsync(db =>
                db.ScriptEvaluateAsync(
                    ghostScript,
                    new RedisKey[] { Consts.QUEUE_HEARTBEATS_KEY, Consts.QUEUE_KEY },
                    new RedisValue[] { cutoffTime }
                ));

            if (removed > 0)
                _logger.LogInformation("[QueueWorker] 유령 유저 {Count}명 제거", removed);

            // 2. 대기열에 사람이 없으면 종료
            long queueLength = await _redisManager.ExecuteAsync(db =>
                db.SortedSetLengthAsync(Consts.QUEUE_KEY));
            if (queueLength == 0) return;

            // 3. 앞에서 N명 pop → Active 키 발급
            var poppedUsers = await _redisManager.ExecuteAsync(db =>
                db.SortedSetPopAsync(Consts.QUEUE_KEY, USERS_PER_SECOND));

            foreach (var user in poppedUsers)
            {
                int userId = (int)user.Element;
                await _redisManager.ExecuteAsync(db =>
                    db.StringSetAsync(
                        $"{Consts.ACTIVE_USER_KEY_PREFIX}{userId}",
                        "1",
                        TimeSpan.FromSeconds(Consts.ACTIVE_USER_TTL_SECONDS)
                    ));
                _logger.LogInformation("[QueueWorker] 입장 허용 — UserId: {UserId} (유효: {TTL}초)",
                    userId, Consts.ACTIVE_USER_TTL_SECONDS);
            }
        }
    }
}
