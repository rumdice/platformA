using PlatformA.Library.Common;
using PlatformA.Library.Core;
using StackExchange.Redis;

namespace PlatformA.Ticketing.API.Services
{
    /// <summary>
    /// 게임 입장 대기열 위한 서비스
    /// </summary>
    public class QueueService
    {
        private readonly RedisManager _redisManager;


        public QueueService(RedisManager redisManager)
        {
            _redisManager = redisManager;
        }

        /// <summary>
        /// 대기열 진입(등록). 줄서기
        /// </summary>
        public async Task<bool> RegisterQueueAsync(int userId)
        {
            var db = _redisManager.Connection.GetDatabase();
            // 현재 시간을 밀리초 단위 숫자로 변환 (이 숫자가 작을수록 먼저 온 사람)
            double timestampScore = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // 입장전 대기열 큐 맥스 사이즈 측정
            long waitQueueLength = await db.SortedSetLengthAsync(Consts.QUEUE_KEY);
            if (waitQueueLength >= Consts.WAIT_QUEUE_MAX_SIZE)
            {
                return false;
            }

            // ZADD: 유저를 큐에 넣음. (이미 큐에 있다면 기존 점수 유지)
            bool isAdded = await db.SortedSetAddAsync(Consts.QUEUE_KEY, userId, timestampScore);
            if (isAdded)
            {
                Console.WriteLine($"[대기열 진입] 유저 {userId} 등록 완료 (Score: {timestampScore})");
                return isAdded;
            }

            
            return isAdded;
        }


        /// <summary>
        /// 대기 순번 조회 (ZRANK) : 내 순번 및 입장 가능 여부 확인
        /// </summary>
        public async Task<long?> GetRankAsync(int userId)
        {
            var db = _redisManager.Connection.GetDatabase();

            // ZRANK: Score가 가장 낮은(먼저 온) 사람부터 0번 인덱스를 부여
            long? rankIndex = await db.SortedSetRankAsync(Consts.QUEUE_KEY, userId);

            // 클라이언트에게 보여줄 때는 1등부터 보여주는 것이 자연스러우므로 +1 처리
            if (rankIndex.HasValue)
            {
                return rankIndex.Value + 1;
            }

            // 대기열에 없으면 null 반환
            return null;
        }


        /// <summary>
        /// 입장 가능한 유저인지 판별
        /// </summary>
        public async Task<bool> IsActiveAsync(int userId)
        {
            var db = _redisManager.Connection.GetDatabase();

            // 이 유저가 입장 가능(Active) 구역에 있는지 확인
            return await db.SetContainsAsync("ticket:active:users", userId);
        }


        public async Task UpdateHeartbeatAsync(int userId)
        {
            var db = _redisManager.Connection.GetDatabase();
            // 현재 시간을 밀리초로 저장
            double currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // 하트비트 전용 ZSET에 생존 시간 갱신
            await db.SortedSetAddAsync("ticket:queue:heartbeats", userId, currentTimestamp);
        }
    }
}