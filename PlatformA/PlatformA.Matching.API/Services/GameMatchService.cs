using Microsoft.AspNetCore.SignalR;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Matching.API.Hubs;
using System.Collections.Concurrent;
using System.Text.Json;

namespace PlatformA.Matching.API.Services
{
    /// <summary>
    /// 게임서버용 매칭 엔진.
    /// </summary>
    public class GameMatchService
    {
        private static int _globalRoomIdCounter = 100; // 발급할 방 번호 (100번부터 시작)

        private readonly IHubContext<MatchingHub> _hubContext;
        private readonly RedisManager _redisManager;
        private readonly ConcurrentQueue<(int UserId, string ConnectionId)> _waitingQueue = new(); // 매칭 대기열 큐


        public GameMatchService(IHubContext<MatchingHub> hubContext, RedisManager redisManager)
        {
            _hubContext = hubContext;
            _redisManager = redisManager;
        }


        /// <summary>
        /// 간단한 1:1 매칭 성사. 들어온 순서대로 매칭이 이루어짐.
        /// </summary>
        public void AddPlayerToQueue(int newUserId, string newConnectionId)
        {
            Console.WriteLine($"[매칭 엔진] 유저 {newUserId} 큐 진입 시도...");

            // 대기열에 누군가 있다면? -> 꺼내서 즉시 매칭!
            if (_waitingQueue.TryDequeue(out var waitingPlayer))
            {
                Console.WriteLine($"[매칭 성사!] 대기자({waitingPlayer.UserId}) vs 신규진입자({newUserId})");

                // 비동기로 매칭 처리 진행 (방 만들고, 알림 쏘고)
                _ = ProcessMatchingAsync(waitingPlayer.UserId, newUserId, waitingPlayer.ConnectionId, newConnectionId);
            }
            else
            {
                // 대기열이 비어있다면? -> 내가 대기열에 들어감
                _waitingQueue.Enqueue((newUserId, newConnectionId));
                Console.WriteLine($"[매칭 엔진] 유저 {newUserId} 대기열 등록 완료. 상대를 기다립니다...");
            }
        }

        private async Task ProcessMatchingAsync(int player1Id, int player2Id, string conn1, string conn2)
        {
            // 1. 방 번호 발급
            int newRoomId = Interlocked.Increment(ref _globalRoomIdCounter);

            MatchSuccessEvent matchEvent = new MatchSuccessEvent
            {
                RoomId = newRoomId,
                MatchedUserIds = new List<int> { player1Id, player2Id }
            };

            // 2. Redis로 게임 서버에 방 생성 명령 발송 : channel:match_success
            string jsonMessage = JsonSerializer.Serialize(matchEvent);
            await _redisManager.GetSubscriber().PublishAsync("channel:match_success", jsonMessage);

            // 3. 매칭된 두 유저에게 SignalR로 결과 알림 발송! : MatchFound
            await _hubContext.Clients.Client(conn1).SendAsync("MatchFound", matchEvent);
            await _hubContext.Clients.Client(conn2).SendAsync("MatchFound", matchEvent);
        }

    }
}
