using PlatformA.Game.Server.Network;
using PlatformA.Library.Core;

namespace PlatformA.Game.Server.Core
{
    /// <summary>
    /// 게임 방 단위 상태 관리자. 입장·퇴장·브로드캐스트 모든 로직은
    /// <see cref="JobQueue"/>를 통해 단일 스레드로 직렬화되므로 별도 lock 없이 안전합니다.
    /// </summary>
    public class GameRoom
    {
        /// <summary>방 고유 ID. Matching.API에서 발급됩니다.</summary>
        public int RoomId { get; set; }

        // 룸에 접속한 유저 목록
        private List<GameSession> _sessions = new List<GameSession>();

        // 이 방의 모든 로직을 한 줄로 세워줄 전담 매니저 (JobQueue)
        private JobQueue _jobQueue = new JobQueue();

        /// <summary>외부 스레드에서 방 로직을 안전하게 예약합니다. 모든 게임 상태 변경은 이 메서드를 통해야 합니다.</summary>
        public void Push(Action job)
        {
            _jobQueue.Push(job);
        }

        // ==========================================
        // 🚨 아래 함수들은 오직 JobQueue 내부에서만 실행됨이 보장됩니다.
        // 따라서 lock 구문 없이도 List를 안전하게 수정할 수 있습니다! (Zero-Lock)
        // ==========================================

        /// <summary>플레이어를 방에 입장시킵니다. JobQueue 내부에서만 호출해야 합니다.</summary>
        public void Enter(GameSession session)
        {
            _sessions.Add(session);
            session.Room = this;
            Console.WriteLine($"[GameRoom] 유저 입장: {session.SessionId} (현재 인원: {_sessions.Count}명)");
        }

        /// <summary>플레이어를 방에서 퇴장시킵니다. JobQueue 내부에서만 호출해야 합니다.</summary>
        public void Leave(GameSession session)
        {
            _sessions.Remove(session);
            session.Room = null;
            Console.WriteLine($"[GameRoom] 유저 퇴장: {session.SessionId} (현재 인원: {_sessions.Count}명)");
        }

        /// <summary>방 내 모든 플레이어에게 패킷을 비동기 전송합니다. Fire-and-Forget 방식으로 각 세션에 순차 발송합니다.</summary>
        public void Broadcast(byte[] packet)
        {
            foreach (var session in _sessions)
            {
                // 🔥 기존 SessionManager 대신, 이 방에 있는 유저에게만 패킷을 보냅니다.
                // '_' 를 붙여서 컴파일러에게 "이 작업이 끝나는 걸 기다리지 않고 버리겠다"고 명시합니다.
                // 스레드는 블로킹 없이 100명에게 순식간에 발송 명령만 내리고 루프를 빠져나옵니다.
                _ = session.SendAsync(packet);
            }
        }

        /// 왜 일케 하면 안되는가??
        // ❌ 게임 서버에서 절대 하면 안 되는 브로드캐스트 방식
        //public async Task Broadcast(byte[] packet)
        //{
        //    foreach (var session in _sessions)
        //    {
        //        // 1번 유저에게 데이터가 '완전히 전송될 때까지' 스레드가 여기서 멈춤 (한명이 조금 렉걸리면 나머지 99999명이 다 멈춰버림)
        //        await session.SendAsync(packet);
        //    }
        //}
    }
}
