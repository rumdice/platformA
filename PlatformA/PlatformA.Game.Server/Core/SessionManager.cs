using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace PlatformA.Game.Server.Core
{
    /// <summary>
    /// 누가 누가 접속했는지 접속한 자들 (Session)을 관리하는 매니저
    /// 전체 서버에 오직 하나만 존재해야 하므로 싱글톤
    /// </summary>
    public class SessionManager
    {
        // 싱글톤 (서버 전체에서 하나의 명부만 공유)
        public static SessionManager Instance { get; } = new SessionManager();

        // 접속 중인 세션들을 담아둘 리스트
        // 🚨 [주의] 여러 유저가 동시에 접속/종료할 수 있으므로 반드시 동시성 제어(Lock)가 필요합니다!
        private readonly HashSet<Session> _sessions = new HashSet<Session>();
        private readonly object _lock = new object();

        // 유저 입장
        public void Add(Session session)
        {
            lock (_lock)
            {
                _sessions.Add(session);
            }
        }

        // 유저 퇴장
        public void Remove(Session session)
        {
            lock (_lock)
            {
                _sessions.Remove(session);
            }
        }

        // 📡 핵심: 모든 유저에게 패킷 쏘기 (브로드캐스팅)
        public void Broadcast(byte[] packet)
        {
            lock (_lock)
            {
                foreach (var session in _sessions)
                {
                    // 각 세션의 SendAsync 호출
                    _ = session.SendAsync(packet);
                }
            }
        }
    }
}
