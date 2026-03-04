using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlatformA.Game.Server.Core
{
    public class GameRoom
    {
        // 테스트용 단일 글로벌 룸 
        // TODO: (나중에는 RoomManager가 여러 방을 관리하게 됩니다)
        public static GameRoom GlobalRoom { get; } = new GameRoom();

        // 룸에 접속한 유저 목록
        private List<GameSession> _sessions = new List<GameSession>();

        // 🔥 이 방의 모든 로직을 한 줄로 세워줄 전담 매니저 (JobQueue)
        private JobQueue _jobQueue = new JobQueue();

        // 외부(스레드 풀)에서 이 방에 일거리를 던질 때 무조건 이 함수를 거쳐야 함
        public void Push(Action job)
        {
            _jobQueue.Push(job);
        }

        // ==========================================
        // 🚨 아래 함수들은 오직 JobQueue 내부에서만 실행됨이 보장됩니다.
        // 따라서 lock 구문 없이도 List를 안전하게 수정할 수 있습니다! (Zero-Lock)
        // ==========================================

        public void Enter(GameSession session)
        {
            _sessions.Add(session);
            // session.Room = this; // (나중을 위해 세션에 방 정보를 남겨둘 수도 있습니다)
            Console.WriteLine($"[GameRoom] 유저 입장: {session.SessionId} (현재 인원: {_sessions.Count}명)");
        }

        public void Leave(GameSession session)
        {
            _sessions.Remove(session);
            Console.WriteLine($"[GameRoom] 유저 퇴장: {session.SessionId} (현재 인원: {_sessions.Count}명)");
        }

        public void Broadcast(byte[] packet)
        {
            foreach (var session in _sessions)
            {
                // 🔥 기존 SessionManager 대신, 이 방에 있는 유저에게만 패킷을 보냅니다.
                // (Session 부모 클래스에 정의된 Send 함수를 호출한다고 가정합니다)
                //session.SendAsync(packet);

                // 어색하게 나오는 녹색 경고는 이렇게 처리.
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
