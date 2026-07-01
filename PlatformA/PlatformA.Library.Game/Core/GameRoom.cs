using PlatformA.Library.Core;
using PlatformA.Library.Game.Network;

namespace PlatformA.Library.Game.Core
{
    /// <summary>
    /// 게임 방 단위 상태 관리자. 입장·퇴장·브로드캐스트 모든 로직은
    /// <see cref="JobQueue"/>를 통해 단일 스레드로 직렬화되므로 별도 lock 없이 안전합니다.
    /// </summary>
    public class GameRoom : IGameRoom
    {
        /// <summary>방 고유 ID. Matching.API에서 발급됩니다.</summary>
        public int RoomId { get; set; }

        private List<GameSession> _sessions = new List<GameSession>();
        private JobQueue _jobQueue = new JobQueue();

        /// <summary>외부 스레드에서 방 로직을 안전하게 예약합니다. 모든 게임 상태 변경은 이 메서드를 통해야 합니다.</summary>
        public void Push(Action job)
        {
            _jobQueue.Push(job);
        }

        /// <summary>플레이어를 방에 입장시킵니다. JobQueue 내부에서만 호출해야 합니다.</summary>
        public virtual void Enter(GameSession session)
        {
            _sessions.Add(session);
            session.Room = this;
            Console.WriteLine($"[GameRoom] 유저 입장: {session.SessionId} (현재 인원: {_sessions.Count}명)");
        }

        /// <summary>플레이어를 방에서 퇴장시킵니다. JobQueue 내부에서만 호출해야 합니다.</summary>
        public virtual void Leave(GameSession session)
        {
            _sessions.Remove(session);
            session.Room = null;
            Console.WriteLine($"[GameRoom] 유저 퇴장: {session.SessionId} (현재 인원: {_sessions.Count}명)");
        }

        /// <summary>방 내 모든 플레이어에게 패킷을 비동기 전송합니다.</summary>
        public virtual void Broadcast(byte[] packet)
        {
            foreach (var session in _sessions)
                _ = session.SendAsync(packet);
        }

        /// <summary>현재 방의 세션 목록을 반환합니다. JobQueue 내부에서만 호출해야 합니다.</summary>
        public IReadOnlyList<GameSession> Sessions => _sessions;
    }
}
