using StackExchange.Redis;

namespace PlatformA.Ticketing.API.Services
{
    public class QueueService
    {
        private readonly IDatabase _db;
        private const string QUEUE_KEY = "queue:iu_concert"; // 대기열 키
        private const int ALLOWED_SIZE = 50; // 한 번에 입장 가능한 인원 (창구 개수)

        public QueueService(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        // 1. 대기열 등록 (줄 서기)
        public async Task RegisterQueue(string userId)
        {
            var score = DateTime.UtcNow.Ticks; // 현재 시간을 점수로 사용 (먼저 온 사람이 1등)
            await _db.SortedSetAddAsync(QUEUE_KEY, userId, score);
        }

        // 2. 내 순번 및 입장 가능 여부 확인
        public async Task<(bool IsAllowed, long Rank)> GetStatus(string userId)
        {
            // 내 등수 확인 (0등부터 시작)
            var rank = await _db.SortedSetRankAsync(QUEUE_KEY, userId);

            if (rank == null)
            {
                // 대기열에 없는 유저
                return (false, -1);
            }

            // 내 등수가 허용 인원(50명) 안에 드는지?
            // 0 ~ 49등까지 입장 가능
            if (rank < ALLOWED_SIZE)
            {
                return (true, rank.Value + 1); // 입장 가능!
            }
            else
            {
                return (false, rank.Value + 1); // 아직 대기 중
            }
        }

        // 3. 퇴장 (볼일 다 봄)
        public async Task LeaveQueue(string userId)
        {
            await _db.SortedSetRemoveAsync(QUEUE_KEY, userId);
        }
    }
}