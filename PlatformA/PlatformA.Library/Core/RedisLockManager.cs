using Polly.CircuitBreaker;
using StackExchange.Redis;
using System.Diagnostics;

namespace PlatformA.Library.Core
{
    public class RedisLockManager
    {
        private readonly RedisManager _redisManager;

        public RedisLockManager(RedisManager redisManager)
        {
            _redisManager = redisManager;
        }

        // 1. 락 획득 시도 (SpinLock 방식)
        public async Task<string?> AcquireLockAsync(string lockKey, TimeSpan expiry, TimeSpan waitTime, TimeSpan retryTime)
        {
            string lockValue = Guid.NewGuid().ToString();
            var sw = Stopwatch.StartNew();

            while (sw.Elapsed < waitTime)
            {
                try
                {
                    // 🔥 핵심: SET NX 명령어 (StackExchange.Redis에서는 When.NotExists로 구현)
                    // 키가 없으면 만들고 true 반환, 있으면 false 반환 (원자적 연산)
                    bool acquired = await _redisManager.ExecuteAsync(
                        db => db.StringSetAsync(lockKey, lockValue, expiry, When.NotExists));

                    if (acquired) return lockValue; // 락 획득 성공! 내 고유 ID를 반환
                }
                catch (BrokenCircuitException)
                {
                    return null; // 회로차단기 OPEN — Redis 응답 불가, 락 획득 불가
                }

                await Task.Delay(retryTime);
            }

            return null; // 대기 시간 초과 (타임아웃)
        }

        // 2. 락 해제 (안전한 해제)
        public async Task ReleaseLockAsync(string lockKey, string lockValue)
        {
            // 🔥 핵심: Lua 스크립트를 사용한 원자적 삭제
            // "락의 값이 내 고유 ID와 같을 때만 지워라"를 한 방에 처리 (Race Condition 방지)
            var script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";

            try
            {
                await _redisManager.ExecuteAsync(db => db.ScriptEvaluateAsync(script,
                    new RedisKey[] { lockKey },
                    new RedisValue[] { lockValue }));
            }
            catch (BrokenCircuitException)
            {
                // 회로차단기 OPEN — 해제 실패. TTL 만료 후 자동 해제됨
            }
        }

        // 3. 락 TTL 갱신 (Lock Heartbeat)
        // 처리 도중 락이 만료되지 않도록 주기적으로 TTL을 연장합니다.
        // 내 lockValue인 경우에만 갱신하여 이미 만료된 락을 실수로 연장하지 않습니다.
        // Returns: true = 갱신 성공, false = 락을 이미 잃음 (만료 후 타 인스턴스가 획득)
        public async Task<bool> RenewLockAsync(string lockKey, string lockValue, TimeSpan expiry)
        {
            var script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('expire', KEYS[1], ARGV[2])
                else
                    return 0
                end";

            try
            {
                var result = (int)await _redisManager.ExecuteAsync(db => db.ScriptEvaluateAsync(script,
                    new RedisKey[] { lockKey },
                    new RedisValue[] { lockValue, (int)expiry.TotalSeconds }));

                return result == 1;
            }
            catch (BrokenCircuitException)
            {
                return false; // 회로차단기 OPEN — 갱신 실패로 처리 (락을 잃은 것으로 간주)
            }
        }
    }
}
