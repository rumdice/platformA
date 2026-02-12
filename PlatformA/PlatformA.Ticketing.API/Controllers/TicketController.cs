using Microsoft.AspNetCore.Mvc;
using PlatformA.Library;
using PlatformA.Ticketing.API.Services;
using RedLockNet.SERedis;
using StackExchange.Redis;

namespace PlatformA.Ticketing.API.Controllers
{
    [ApiController]
    [Route("api/tickets")]
    public class TicketController : ControllerBase
    {
        private readonly IDatabase _redis;
        private readonly RedLockFactory _lockFactory; // 락 공장
        private readonly RedisLockManager _lockManager; // 수동 락 매니저

        private readonly QueueService _queueService; // 대기열 서비스

        // static으로 선언하여 서버 내 모든 요청이 이 자물쇠를 공유함
        private static readonly SemaphoreSlim _localLock = new SemaphoreSlim(1, 1);

        public TicketController(IConnectionMultiplexer redis, RedLockFactory lockFactory, RedisLockManager lockManager, QueueService queueService)
        {
            _redis = redis.GetDatabase();
            _lockFactory = lockFactory;
            _lockManager = lockManager;
            _queueService = queueService;
        }

        // 0. (준비) 티켓 수량 초기화 API
        // POST /api/tickets/reset?count=100
        [HttpPost("reset")]
        public async Task<IActionResult> Reset(int count = 100)
        {
            await _redis.StringSetAsync("ticket:iu_concert", count);
            return Ok($"아이유 콘서트 티켓 {count}장 발행 완료!");
        }

        // 1. (취약함) 티켓 예매 API
        // POST /api/tickets/buy-bad
        [HttpPost("buy-bad")]
        public async Task<IActionResult> BuyTicketBad()
        {
            var key = "ticket:iu_concert";

            // ---------------------------------------------------------
            // 🚨 [Race Condition 발생 구간]
            // Redis의 Atomic 명령어(Decrement)를 쓰지 않고, 
            // 일부러 값을 가져와서 C# 메모리에서 계산 후 덮어씁니다.
            // ---------------------------------------------------------

            // 1. 재고 확인 (Read)
            var currentStockValue = await _redis.StringGetAsync(key);
            int currentStock = (int)currentStockValue;

            if (currentStock <= 0)
            {
                return BadRequest("매진되었습니다.");
            }

            // TODO: (일부러 틈을 벌리기 위해 아주 짧은 지연 시간 추가 - 실제 DB I/O 시뮬레이션)
            await Task.Delay(10);

            // 2. 재고 차감 (Modify)
            int newStock = currentStock - 1;

            // 3. 저장 (Write)
            await _redis.StringSetAsync(key, newStock);

            return Ok($"예매 성공! 남은 표: {newStock}");
        }


        // 2. (안전함) 분산 락 적용 예매 API
        // POST /api/tickets/buy-good
        [HttpPost("buy-good")]
        public async Task<IActionResult> BuyTicketGood()
        {
            var key = "ticket:iu_concert";
            var lockKey = "lock:ticket:iu_concert"; // 락을 위한 별도 키

            // 🔥 1. 락 획득 시도
            // resource: 락 이름
            // expiryTime: 락 자동 만료 시간 (3초 지나면 강제 반납 - 데드락 방지)
            // waitTime: 락을 얻기 위해 기다리는 시간 (2초 동안 계속 노크함)
            // retryTime: 노크하는 간격 (0.1초마다 노크)
            using (var redLock = await _lockFactory.CreateLockAsync(lockKey, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(100)))
            {
                if (redLock.IsAcquired)
                {
                    // 🔒 [임계 구역 (Critical Section)] - 한 번에 한 명만 들어옴

                    // A. 재고 확인
                    var currentStockValue = await _redis.StringGetAsync(key);
                    int currentStock = (int)currentStockValue;

                    if (currentStock <= 0)
                    {
                        return BadRequest("매진되었습니다. (Safe)");
                    }

                    // (아까와 똑같은 딜레이를 줘도 안전한지 테스트)
                    await Task.Delay(10);

                    // B. 재고 차감
                    int newStock = currentStock - 1;

                    // C. 저장
                    await _redis.StringSetAsync(key, newStock);

                    return Ok($"예매 성공! 남은 표: {newStock}");

                    // (using 블록이 끝나면 자동으로 락 반납 - Unlock)
                }
                else
                {
                    // 락 획득 실패 (대기 시간 초과)
                    return StatusCode(429, "접속자가 너무 많아 실패했습니다. 다시 시도해주세요.");
                }
            }
        }


        // 2. (안전함) 분산 락 적용 예매 API
        // POST /api/tickets/buy-good-manual
        [HttpPost("buy-good-manual")]
        public async Task<IActionResult> BuyTicketGoodManual()
        {
            var key = "ticket:iu_concert";
            var lockKey = "lock:ticket:iu_concert"; // 락을 위한 별도 키

            // 🔥 1. 락 획득 시도
            // (유효기간 3초, 최대 2초 대기, 0.1초 간격 재시도)
            string? lockValue = await _lockManager.AcquireLockAsync(
                lockKey,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromMilliseconds(100));

            if (lockValue == null)
            {
                return StatusCode(429, "접속자가 너무 많아 대기 시간 초과 (수동 락).");
            }

            try
            {
                // 🔒 [임계 구역 (Critical Section)]
                // A. 재고 확인
                var currentStockValue = await _redis.StringGetAsync(key);
                int currentStock = (int)currentStockValue;

                if (currentStock <= 0)
                {
                    return BadRequest("매진되었습니다.");
                }

                // (아까와 똑같은 딜레이를 줘도 안전한지 테스트)
                await Task.Delay(10);

                // B. 재고 차감
                int newStock = currentStock - 1;

                // C. 저장
                await _redis.StringSetAsync(key, newStock);

                return Ok($"예매 성공! 남은 표: {newStock}");
            }
            finally
            {
                // 2. 락 해제 (예외가 발생하더라도 반드시 해제되도록 finally 블록 사용)
                await _lockManager.ReleaseLockAsync(lockKey, lockValue);
            }

        }


        // 2. (안전함) 분산 락 적용 로컬 
        // POST /api/tickets/buy-good-manual-single
        // [주의!!] 하지만 현업에서는 이렇게 싱글락을 사용할 수 없다. 왜냐? - 티켓팅 서버가 한대일때만 가능함.
        // 티켓팅 서버가 여러대일때는 여러대의 서버를 공유하는 메모리가 없기 때문에 티켓팅이 엉망이 된다.
        // 그래서 서버 공유 메모리인 레디스를 활용 하는 것.
        // 이 SemaphoreSlim 과 소켓 통신을 사용하여 일종의 서버들끼리 통신과 메모리를 공유하는 LockManagerServer 를 만들수는 있지만
        // 결국 Redis의 하위 호환인 꼴이다.
        [HttpPost("buy-good-manual-single")]
        public async Task<IActionResult> BuyTicketGoodManualSingle()
        {
            var key = "ticket:iu_concert";
            var lockKey = "lock:ticket:iu_concert"; // 락을 위한 별도 키

            // 1. 메모리 락 획득 (대기)
            await _localLock.WaitAsync();

            try
            {
                // 🔒 [임계 구역 (Critical Section)]
                // A. 재고 확인
                var currentStockValue = await _redis.StringGetAsync(key);
                int currentStock = (int)currentStockValue;

                if (currentStock <= 0)
                {
                    return BadRequest("매진되었습니다.");
                }

                // (아까와 똑같은 딜레이를 줘도 안전한지 테스트)
                await Task.Delay(10);

                // B. 재고 차감
                int newStock = currentStock - 1;

                // C. 저장
                await _redis.StringSetAsync(key, newStock);

                return Ok($"예매 성공! 남은 표: {newStock}");
            }
            finally
            {
                // 2. 락 해제 (예외가 발생하더라도 반드시 해제되도록 finally 블록 사용)
                _localLock.Release();
            }

        }


        // 3. (Lock-Free) Lua Script를 이용한 원자적 차감
        // POST /api/tickets/buy-lockfree
        [HttpPost("buy-lockfree")]
        public async Task<IActionResult> BuyTicketLockFree()
        {
            var key = "ticket:iu_concert";

            // 🔥 Lua Script 작성
            // KEYS[1]: 티켓 키 이름 ("ticket:iu_concert")
            // 로직:
            // 1. 현재 재고를 가져온다 (tonumber로 숫자 변환)
            // 2. 재고가 0보다 크면? -> decr(감소) 수행 후 남은 수량 리턴
            // 3. 재고가 없으면? -> -1 리턴
            var script = @"
                local current_stock = tonumber(redis.call('get', KEYS[1]))
                if current_stock > 0 then
                    redis.call('decr', KEYS[1])
                    return current_stock - 1
                else
                    return -1
                end
            ";

            try
            {
                // ScriptEvaluateAsync를 통해 스크립트 전송 및 실행
                var result = await _redis.ScriptEvaluateAsync(script, new RedisKey[] { key });

                // 리턴값 확인
                int remainingStock = (int)result;

                if (remainingStock >= 0)
                {
                    return Ok($"[Lock-Free] 예매 성공! 남은 표: {remainingStock}");
                }
                else
                {
                    return BadRequest("[Lock-Free] 매진되었습니다.");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Redis 에러 발생");
            }
        }


        // 4. (Lock-Free) Redis 감소 연산을 활용
        // POST /api/tickets/buy-lockfree
        [HttpPost("buy-decr")]
        public async Task<IActionResult> BuyTicketDecr()
        {
            var key = "ticket:iu_concert";

            // 1. 일단 깎아! (Atomic)
            long remained = await _redis.StringDecrementAsync(key);

            // 2. 결과 확인
            if (remained >= 0)
            {
                // 성공! (0개 남았을 때 내가 마지막 가져감)
                return Ok($"예매 성공! 남은 표: {remained}");
            }
            else
            {
                // 3. 실패! (마이너스 통장이 됨) -> 다시 채워놓자 (보상 트랜잭션)
                // 비동기로 처리해도 됨 (Fire and Forget)
                await _redis.StringIncrementAsync(key);

                return BadRequest("매진되었습니다.");
            }
        }


        // 3. (Lock-Free) Lua Script를 이용한 원자적 차감 - 대기열 적용
        // POST /api/tickets/buy-final
        [HttpPost("buy-final")]
        public async Task<IActionResult> BuyTicketFinal(string _userId)
        {
            var (isAllowed, _) = await _queueService.GetStatus(_userId);

            if (!isAllowed)
            {
                return StatusCode(403, "아직 입장 순서가 아닙니다. 대기열 상태를 확인해주세요.");
            }

            var key = "ticket:iu_concert";

            // 🔥 Lua Script 작성
            // KEYS[1]: 티켓 키 이름 ("ticket:iu_concert")
            // 로직:
            // 1. 현재 재고를 가져온다 (tonumber로 숫자 변환)
            // 2. 재고가 0보다 크면? -> decr(감소) 수행 후 남은 수량 리턴
            // 3. 재고가 없으면? -> -1 리턴
            var script = @"
                    local val = redis.call('get', KEYS[1])
                    if val == false then return -1 end
                    local current = tonumber(val)
                    if current > 0 then
                        redis.call('decr', KEYS[1])
                        return current - 1
                    else
                        return -1
                    end
                ";

            try
            {
                // ScriptEvaluateAsync를 통해 스크립트 전송 및 실행
                var result = await _redis.ScriptEvaluateAsync(script, new RedisKey[] { key });

                // 리턴값 확인
                int remainingStock = (int)result;

                if (remainingStock >= 0)
                {
                    // 🎉 2. 성공 시 대기열에서 퇴장 (뒷사람에게 자리 양보)
                    await _queueService.LeaveQueue(_userId);
                    return Ok($"예매 성공! 남은 표: {result}");
                }
                else
                {
                    // 매진 시에도 퇴장시켜야 할까? -> 정책 나름이지만 보통 퇴장시킴
                    await _queueService.LeaveQueue(_userId);
                    return BadRequest("매진되었습니다.");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Redis 에러 발생");
            }
        }
    }
}
