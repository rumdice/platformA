
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
                    // 1. 대기열(ZSET)에 사람이 있는지 확인
                    long queueLength = await db.SortedSetLengthAsync(Consts.QUEUE_KEY);

                    if (queueLength > 0)
                    {
                        // 2. ZPOPMIN: 줄의 맨 앞(가장 점수가 낮은)에서 N명을 뽑아(Pop)냅니다!
                        // 뽑아냄과 동시에 ZSET에서는 삭제됩니다.
                        var poppedUsers = await db.SortedSetPopAsync(Consts.QUEUE_KEY, USERS_PER_SECOND);

                        foreach (var user in poppedUsers)
                        {
                            int userId = (int)user.Element;

                            // 3. 뽑아낸 유저들을 '입장 가능(Active)' 리스트(Set)에 넣어줍니다.
                            await db.SetAddAsync(Consts.ACTIVE_KEY, userId);

                            Console.WriteLine($"[입장 허용 🟢] 유저 {userId}님이 대기열을 통과했습니다!");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[QueueWorker] 에러 발생: {ex.Message}");
                    await Task.Delay(2000, stoppingToken);
                }
            }

            throw new NotImplementedException();
        }
    }
}
