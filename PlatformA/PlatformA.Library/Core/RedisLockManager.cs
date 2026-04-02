using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlatformA.Library.Core
{
    public class RedisLockManager
    {
        private readonly IDatabase _db;

        public RedisLockManager(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        // 1. 락 획득 시도 (SpinLock 방식)
        public async Task<string?> AcquireLockAsync(string lockKey, TimeSpan expiry, TimeSpan waitTime, TimeSpan retryTime)
        {
            // 나만의 고유한 락 키값 (나중에 풀 때 내 것인지 확인하기 위함)
            string lockValue = Guid.NewGuid().ToString();
            var sw = Stopwatch.StartNew();

            while (sw.Elapsed < waitTime)
            {
                // 🔥 핵심: SET NX 명령어 (StackExchange.Redis에서는 When.NotExists로 구현)
                // 키가 없으면 만들고 true 반환, 있으면 false 반환 (원자적 연산)
                bool acquired = await _db.StringSetAsync(lockKey, lockValue, expiry, When.NotExists);

                if (acquired)
                {
                    return lockValue; // 락 획득 성공! 내 고유 ID를 반환
                }

                // 락 획득 실패 시 잠시 대기 후 재시도 (Spin 방지)
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

            await _db.ScriptEvaluateAsync(script,
                new RedisKey[] { lockKey },
                new RedisValue[] { lockValue }
            );
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

            var result = (int)await _db.ScriptEvaluateAsync(script,
                new RedisKey[] { lockKey },
                new RedisValue[] { lockValue, (int)expiry.TotalSeconds }
            );

            return result == 1;
        }
    }
}