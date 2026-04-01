using Microsoft.AspNetCore.SignalR;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Matching.API.Hubs;
using System.Text.Json;

namespace PlatformA.Matching.API.Services
{
    /// <summary>
    /// 게임서버용 매칭 엔진.
    /// </summary>
    public class GameMatchService : BackgroundService
    {
        //private static int _globalRoomIdCounter = 100; // 발급할 방 번호 (100번부터 시작)

        private readonly IHubContext<MatchingHub> _hubContext;
        private readonly RedisManager _redisManager;
        private const string MATCH_QUEUE_KEY = "queue:gamematch:1v1";

        public GameMatchService(IHubContext<MatchingHub> hubContext, RedisManager redisManager)
        {
            _hubContext = hubContext;
            _redisManager = redisManager;
        }


        /// <summary>
        /// 매칭 큐에 넣는다.
        /// </summary>
        public async Task AddPlayerToQueueAsync(int userId)
        {
            Console.WriteLine($"[매칭 엔진] 유저 {userId} 큐 진입 시도...");

            var db = _redisManager.Connection.GetDatabase();
            await db.ListRightPushAsync(MATCH_QUEUE_KEY, userId);

            Console.WriteLine($"[매칭] 유저 {userId} Redis 대기열 큐 진입 완료.");
        }


        /// <summary>
        /// 백그라운드 워커 : 매칭 시킨다.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var db = _redisManager.Connection.GetDatabase();
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try 
                {
                    // Redis List에 대기 중인 유저가 2명 이상인지 확인
                    long queueLength = await db.ListLengthAsync(MATCH_QUEUE_KEY);

                    if (queueLength >= 2)
                    {
                        // 큐의 맨 앞(Left)에서 두 명을 뽑아옵니다 (LPOP)
                        var user1Val = await db.ListLeftPopAsync(MATCH_QUEUE_KEY);
                        var user2Val = await db.ListLeftPopAsync(MATCH_QUEUE_KEY);

                        if (user1Val.HasValue && user2Val.HasValue)
                        {
                            int player1Id = (int)user1Val;
                            int player2Id = (int)user2Val;

                            // 매칭 성사 비동기 처리
                            _ = ProcessMatchingAsync(player1Id, player2Id);
                        }
                        else
                        {
                            // 대기자가 부족하면 DB 부하를 막기 위해 1초 대기 (Polling 간격)
                            await Task.Delay(1000, stoppingToken);
                        }
                    }
                    else
                    {
                        // 대기자가 부족하면 DB 부하를 막기 위해 1초 대기 (Polling 간격)
                        await Task.Delay(1000, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[매칭 엔진] 매칭 처리 중 오류 발생: {ex.Message}");
                    await Task.Delay(2000, stoppingToken);
                }
            }
        }


        /// <summary>
        /// 매칭을 성사 시키는 로직
        /// </summary>
        private async Task ProcessMatchingAsync(int player1Id, int player2Id)
        {
            // 1. 방 번호 발급 (Redis INCR 사용, 1번 방 제외)
            var db = _redisManager.Connection.GetDatabase();
            int newRoomId = (int)await db.StringIncrementAsync("global:room_id");

            // 1번 방은 광장으로 사용하므로 건너뜀
            if (newRoomId == 1)
            {
                newRoomId = (int)await db.StringIncrementAsync("global:room_id");
            }

            Console.WriteLine($"[매칭 성사!] 방({newRoomId}) : 유저 {player1Id} vs 유저 {player2Id}");

            var matchEvent = new MatchSuccessEvent
            {
                RoomId = newRoomId,
                MatchedUserIds = new List<int> { player1Id, player2Id }
            };

            // 메시지 생성.
            string jsonMessage = JsonSerializer.Serialize(matchEvent);

            // 2. Redis로 게임 서버에 방 생성 명령 발송 : channel:match_success
            await _redisManager.GetSubscriber().PublishAsync("channel:match_success", jsonMessage);

            // 3. 매칭된 두 유저에게 SignalR로 결과 알림 발송! : MatchFound.
            // ConnectionId를 몰라도 Group 이름을 알고 있음.
            await _hubContext.Clients.Group($"User_{player1Id}").SendAsync("MatchFound", matchEvent);
            await _hubContext.Clients.Group($"User_{player2Id}").SendAsync("MatchFound", matchEvent);
        }

        
    }
}
