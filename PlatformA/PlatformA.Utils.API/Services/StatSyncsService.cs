
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace PlatformA.Utils.API.Services
{
    public class StatSyncsService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConnectionMultiplexer _redis; // 컨트롤러에서의 IDatabase 대신 IConnectionMultiplexer 사용 (연결 그 자체)
        private readonly ILogger<StatSyncsService> _logger;

        public StatSyncsService(IServiceProvider serviceProvider, IConnectionMultiplexer redis, ILogger<StatSyncsService> logger)
        {
            _serviceProvider = serviceProvider;
            _redis = redis;
            _logger = logger;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 서버가 꺼질 때까지 무한루프
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 1. 5초마다 깨어남 (배치 주기)
                    await Task.Delay(5000, stoppingToken);
                    await SyncStatsToDb();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "통계 동기화 중 에러 발생");
                }
            }
        }

        private async Task SyncStatsToDb()
        {
            var dbRedis = _redis.GetDatabase();

            // 2. "변경된 놈들 명단(dirty_codes)" 가져오기
            // SMEMBERS 명령어: Set에 있는 모든 멤버 조회
            var dirtyCodes = await dbRedis.SetMembersAsync("dirty_codes");

            if (dirtyCodes.Length == 0) return; // 변경된 게 없으면 다시 잠듦

            _logger.LogInformation($"[Sync] {dirtyCodes.Length}개의 URL 통계 DB 반영 시작...");

            // 3. DB 접속 (BackgroundService는 Singleton이라 Scoped인 DB를 쓰려면 Scope를 직접 만들어야 함)
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                foreach (var redisKey in dirtyCodes)
                {
                    string code = redisKey.ToString();

                    // Redis에서 현재 조회수 가져오기
                    var currentCountVal = await dbRedis.StringGetAsync($"stats:{code}");

                    if (currentCountVal.HasValue && int.TryParse(currentCountVal, out int count))
                    {
                        // DB 업데이트 (이 부분은 벌크 업데이트로 더 최적화 가능하지만 일단 루프로 처리)
                        var urlItem = await dbContext.ShortUrls.FirstOrDefaultAsync(u => u.Code == code);
                        if (urlItem != null)
                        {
                            urlItem.ClickCount = count; // 덮어쓰기 (Redis가 진실임)
                        }
                    }

                    // 4. 처리 끝난 놈은 명단에서 지우기
                    // 주의: 전체 삭제(DEL)하지 말고, 처리한 놈만 지워야(SREM) 동시성 이슈가 없음
                    await dbRedis.SetRemoveAsync("dirty_codes", code);
                }

                // 5. DB에 진짜 저장 (Commit)
                await dbContext.SaveChangesAsync();
            }

            _logger.LogInformation("[Sync] DB 반영 완료!");
        }
    }
}
