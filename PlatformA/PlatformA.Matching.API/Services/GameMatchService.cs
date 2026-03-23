using Microsoft.AspNetCore.SignalR;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Matching.API.Hubs;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text.Json;

namespace PlatformA.Matching.API.Services
{
    // 클라이언트의 요청을 매칭
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


        //public async Task ProcessMatchingAsync(int player1Id, int player2Id)
        //{
        //    // 1. 매칭 성사 로직 완료
        //    Console.WriteLine($"[MatchingEngine] 유저 {player1Id} 와 {player2Id} 매칭 성사!");

        //    int newRoomId = Interlocked.Increment(ref _globalRoomIdCounter);

        //    // 🚀 2. 게임 서버로 보낼 편지(DTO) 작성
        //    MatchSuccessEvent matchEvent = new MatchSuccessEvent
        //    {
        //        RoomId = newRoomId,
        //        MatchedUserIds = new List<int> { player1Id, player2Id }
        //    };

        //    string jsonMessage = JsonSerializer.Serialize(matchEvent);

        //    // 🚀 3. Redis 확성기로 "channel:match_success" 채널에 소리치기!
        //    ISubscriber pub = _redisManager.GetSubscriber();
        //    await pub.PublishAsync("channel:match_success", jsonMessage);

        //    Console.WriteLine($"[MatchingEngine] 게임 서버에 {newRoomId}번 방 생성 명령 발송 완료!");

        //    // (이후 유저 1, 2 에게 "너희 매칭됐어! 게임서버 IP와 RoomId(newRoomId)로 접속해!" 라고 응답을 내려줌)
        //    await _hubContext.Clients.Group($"User_{player1Id}").SendAsync("MatchFound", matchEvent);
        //    await _hubContext.Clients.Group($"User_{player2Id}").SendAsync("MatchFound", matchEvent);

        //    Console.WriteLine($"[SignalR] 유저 {player1Id}, {player2Id}에게 매칭 성공 알림 발송");
        //}

        private async Task ProcessMatchingAsync(int player1Id, int player2Id, string conn1, string conn2)
        {
            // 1. 방 번호 발급 (기존 로직)
            int newRoomId = Interlocked.Increment(ref _globalRoomIdCounter);

            MatchSuccessEvent matchEvent = new MatchSuccessEvent
            {
                RoomId = newRoomId,
                MatchedUserIds = new List<int> { player1Id, player2Id }
            };

            // 2. Redis로 게임 서버에 방 생성 명령 발송 (기존 로직)
            string jsonMessage = JsonSerializer.Serialize(matchEvent);
            await _redisManager.GetSubscriber().PublishAsync("channel:match_success", jsonMessage);

            // 3. 🚀 매칭된 두 유저에게 SignalR로 결과 알림 발송!
            await _hubContext.Clients.Client(conn1).SendAsync("MatchFound", matchEvent);
            await _hubContext.Clients.Client(conn2).SendAsync("MatchFound", matchEvent);
        }


        public void AddPlayerToQueue(int newUserId, string newConnectionId)
        {
            Console.WriteLine($"[매칭 엔진] 유저 {newUserId} 큐 진입 시도...");

            // 1. 대기열에 누군가 있다면? -> 꺼내서 즉시 매칭!
            if (_waitingQueue.TryDequeue(out var waitingPlayer))
            {
                Console.WriteLine($"[매칭 성사!] 대기자({waitingPlayer.UserId}) ❤️ 신규진입자({newUserId})");

                // 비동기로 매칭 처리 진행 (방 만들고, 알림 쏘고)
                _ = ProcessMatchingAsync(waitingPlayer.UserId, newUserId, waitingPlayer.ConnectionId, newConnectionId);
            }
            else
            {
                // 2. 대기열이 비어있다면? -> 내가 대기열에 들어감
                _waitingQueue.Enqueue((newUserId, newConnectionId));
                Console.WriteLine($"[매칭 엔진] 유저 {newUserId} 대기열 등록 완료. 상대를 기다립니다...");
            }
        }
    }
}
