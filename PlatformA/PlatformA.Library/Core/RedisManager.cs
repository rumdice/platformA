using PlatformA.Library.Common;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PlatformA.Library.Core
{
    public class RedisManager
    {
        public static RedisManager Instance { get; } = new RedisManager();

        private ConnectionMultiplexer _redis;
        public RedisLockManager LockManager { get; private set; }

        // 🚀 추가: Pub/Sub 메시지를 엿들을 수신기
        private ISubscriber _subscriber;

        // 🚀 핵심: 매칭 성공 메시지가 도착했을 때 외부(서버)로 알려줄 이벤트!
        public event Action<MatchSuccessEvent> OnMatchSuccessReceived;

        private RedisManager() { }

        public void Init(string connectionString = "127.0.0.1:6379")
        {
            // TODO: local only
            // docker run --name my-redis -p 6379:6379 -d redis 로컬 환경에서 설치 필요.
            try
            {
                // Redis 서버 연결
                _redis = ConnectionMultiplexer.Connect(connectionString);

                // 개발자님이 만들어두신 RedisLockManager 연동
                LockManager = new RedisLockManager(_redis);
                
                // 🚀 추가: 수신기를 할당받고, 특정 채널 구독 시작!
                _subscriber = _redis.GetSubscriber();
                SubscribeToMatchingEvents();

                Console.WriteLine($"[RedisManager] Redis 연결 및 LockManager 초기화 성공! ({connectionString})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RedisManager] Redis 연결 실패: {ex.Message}");
            }
        }


        // 🚀 매칭 채널 구독 로직
        private void SubscribeToMatchingEvents()
        {
            // 채널 이름 (발행하는 쪽과 글자가 완벽히 똑같아야 함)
            string channelName = "channel:match_success";

            _subscriber.Subscribe(channelName, (channel, message) =>
            {
                // 메시지가 도착하면 백그라운드 스레드에서 이 콜백이 실행됩니다.
                try
                {
                    // 1. 편지(JSON)를 뜯어서 우리가 아는 DTO로 변환
                    var matchEvent = JsonSerializer.Deserialize<MatchSuccessEvent>(message);

                    if (matchEvent != null)
                    {
                        Console.WriteLine($"\n[Redis PubSub] 📡 매칭 성사 수신! -> 지시받은 방 번호: {matchEvent.RoomId}");
                        Console.WriteLine($"[Redis PubSub] 📡 입장 예정 유저들: {string.Join(", ", matchEvent.MatchedUserIds)}");

                        // 2. 명령대로 즉시 빈 방을 파놓고 대기!
                        // TODO: GameRoomManager를 사용 할 수 없음
                        //GameRoomManager.Instance.CreateRoom(matchEvent.RoomId);

                        // 🚀 하위 라이브러리는 방을 어떻게 만드는지 모릅니다.
                        // 그저 "메시지 왔어요!" 하고 이벤트를 발생(Invoke)시킬 뿐입니다.
                        OnMatchSuccessReceived?.Invoke(matchEvent);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Redis PubSub Error] 메시지 처리 실패: {ex.Message}");
                }
            });
        }


        public ISubscriber GetSubscriber()
        {
            return _subscriber;
        }

    }
}
