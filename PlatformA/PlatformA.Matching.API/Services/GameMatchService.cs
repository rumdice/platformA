using PlatformA.Library.Common;
using StackExchange.Redis;
using System.Text.Json;

namespace PlatformA.Matching.API.Services
{
    // 클라이언트의 요청을 매칭
    public class GameMatchService
    {
        private readonly IConnectionMultiplexer _redis;
        private static int _globalRoomIdCounter = 100; // 발급할 방 번호 (100번부터 시작)

        public GameMatchService(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }


        public async Task ProcessMatchingAsync(int player1Id, int player2Id)
        {
            // 1. 매칭 성사 로직 완료
            Console.WriteLine($"[MatchingEngine] 유저 {player1Id} 와 {player2Id} 매칭 성사!");

            int newRoomId = Interlocked.Increment(ref _globalRoomIdCounter);

            // 🚀 2. 게임 서버로 보낼 편지(DTO) 작성
            MatchSuccessEvent matchEvent = new MatchSuccessEvent
            {
                RoomId = newRoomId,
                MatchedUserIds = new List<int> { player1Id, player2Id }
            };

            string jsonMessage = JsonSerializer.Serialize(matchEvent);

            // 🚀 3. Redis 확성기로 "channel:match_success" 채널에 소리치기!
            ISubscriber pub = _redis.GetSubscriber();
            await pub.PublishAsync("channel:match_success", jsonMessage);

            Console.WriteLine($"[MatchingEngine] 게임 서버에 {newRoomId}번 방 생성 명령 발송 완료!");

            // (이후 유저 1, 2 에게 "너희 매칭됐어! 게임서버 IP와 RoomId(newRoomId)로 접속해!" 라고 응답을 내려줌)
        }
    }
}
