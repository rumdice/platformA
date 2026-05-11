using Microsoft.AspNetCore.SignalR;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Ticketing.API.Hubs;
using StackExchange.Redis;

namespace PlatformA.Ticketing.API.Workers
{
    public class QueueWorkerService : BackgroundService
    {
        private readonly RedisManager _redisManager;
        private readonly IHubContext<QueueHub> _hubContext;
        private readonly ILogger<QueueWorkerService> _logger;

        private readonly int _baseRate = Consts.QUEUE_BASE_RATE;
        private readonly int _maxRate = Consts.QUEUE_MAX_RATE;
        private const int SCALE_THRESHOLD = 200; // 대기열 N명당 처리 속도 1단계 증가
        private const string WORKER_LEADER_LOCK = "lock:queue:worker:leader";
        private static readonly TimeSpan LOCK_EXPIRY = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan LOCK_RENEW_INTERVAL = TimeSpan.FromSeconds(3);

        public QueueWorkerService(RedisManager redisManager, IHubContext<QueueHub> hubContext, ILogger<QueueWorkerService> logger)
        {
            _redisManager = redisManager;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[QueueWorker] 티켓팅 문지기 워커 가동! 기준: {Base}명/초, 최대: {Max}명/초",
                _baseRate, _maxRate);

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

        // 대기열 길이에 비례해 처리 속도를 조정합니다.
        // effectiveRate = min(baseRate × (1 + queueLength / SCALE_THRESHOLD), maxRate)
        private int CalculateEffectiveRate(long queueLength)
        {
            int multiplier = 1 + (int)(queueLength / SCALE_THRESHOLD);
            return (int)Math.Min((long)_baseRate * multiplier, _maxRate);
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
            if (queueLength == 0)
                return;

            // 3. 앞에서 N명 pop → Active 키 발급 (대기열 길이에 따라 처리 속도 동적 조정)
            int effectiveRate = CalculateEffectiveRate(queueLength);
            if (effectiveRate != _baseRate)
                _logger.LogInformation("[QueueWorker] 처리 속도 동적 조정: {Rate}명/초 (대기열 {QLen}명)",
                    effectiveRate, queueLength);

            var poppedUsers = await _redisManager.ExecuteAsync(db =>
                db.SortedSetPopAsync(Consts.QUEUE_KEY, effectiveRate));


            // REVIEW: 딥다이브 리뷰 2-2 개선 포인트. (대기열을 초당 50명이 아니라 1000명으로 늘어난다면?)
            //foreach (var user in poppedUsers)
            //{
            //    int userId = (int)user.Element;
            //    try
            //    {
            //        await _redisManager.ExecuteAsync(db =>
            //            db.StringSetAsync(
            //                $"{Consts.ACTIVE_USER_KEY_PREFIX}{userId}",
            //                "1",
            //                TimeSpan.FromSeconds(Consts.ACTIVE_USER_TTL_SECONDS)
            //            ));
            //        _logger.LogInformation("[QueueWorker] 입장 허용 — UserId: {UserId} (유효: {TTL}초)",
            //            userId, Consts.ACTIVE_USER_TTL_SECONDS);

            //        // SignalR push: 연결 중인 클라이언트에게 즉시 알림
            //        await _hubContext.Clients
            //            .Group($"User_{userId}")
            //            .SendAsync("QueueActivated");
            //    }
            //    catch (Exception ex)
            //    {
            //        // Active 키 설정 실패 → 큐에서 꺼냈지만 처리 못한 유저를 재큐잉
            //        _logger.LogError(ex, "[QueueWorker] Active 키 설정 실패 — UserId: {UserId}, 재큐잉", userId);
            //        try
            //        {
            //            double score = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            //            await _redisManager.ExecuteAsync(db =>
            //                db.SortedSetAddAsync(Consts.QUEUE_KEY, userId, score));
            //        }
            //        catch (Exception reEnqueueEx)
            //        {
            //            _logger.LogError(reEnqueueEx, "[QueueWorker] 재큐잉도 실패 — UserId: {UserId} 유실", userId);
            //        }
            //    }
            //}

            var activeTasks = poppedUsers.Select(async user =>
            {
                int userId = (int)user.Element;
                try
                {
                    await _redisManager.ExecuteAsync(db =>
                        db.StringSetAsync(
                            $"{Consts.ACTIVE_USER_KEY_PREFIX}{userId}",
                            "1",
                            TimeSpan.FromSeconds(Consts.ACTIVE_USER_TTL_SECONDS)
                        ));

                    _logger.LogInformation("[QueueWorker] 입장 허용 — UserId: {UserId} (유효: {TTL}초)",
                        userId, Consts.ACTIVE_USER_TTL_SECONDS);

                    return (userId, success: true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[QueueWorker] Active 키 설정 실패 — UserId: {UserId}, 재큐잉", userId);

                    return (userId, success: false);
                }
            }).ToList();

            // Task.WhenAll은 절대 throw하지 않음 (각 Task가 예외를 내부 처리)
            var results = await Task.WhenAll(activeTasks);

            // 성공 실패 분리 처리
            var succeeded = results.Where(r => r.success).ToList();
            var failed = results.Where(r => !r.success).ToList();

            // 성공 유저 SignalR 알림
            if (succeeded.Any())
            {
                var groupNames = succeeded.Select(r =>
                    $"User_{r.userId}").ToList();
                await _hubContext.Clients.Groups(groupNames).SendAsync("QueueActivated");
            }

            // Active 키 설정 실패 → 큐에서 꺼냈지만 처리 못한 유저를 재큐잉
            if (failed.Any())
            {
                var reQueueTasks = failed.Select(async r =>
                {
                    try
                    {
                        double score = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        await _redisManager.ExecuteAsync(db =>
                            db.SortedSetAddAsync(Consts.QUEUE_KEY, r.userId, score));

                        _logger.LogInformation("[QueueWorker] 재큐잉 성공 — UserId: {UserId} (유효: {TTL}초)",
                            r.userId, Consts.ACTIVE_USER_TTL_SECONDS);
                    }
                    catch (Exception reEnqueueEx)
                    {
                        _logger.LogError(reEnqueueEx, "[QueueWorker] 재큐잉도 실패 — UserId: {UserId} 유실", r.userId);
                    }
                });
                await Task.WhenAll(reQueueTasks);
            }
        }
    }
}
