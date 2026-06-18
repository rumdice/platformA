using System.Net;
using PlatformA.Library.Core;
using PlatformA.Library.Game.Core;
using PlatformA.Library.Network;

namespace PlatformA.Library.Game.Network
{
    /// <summary>
    /// 게임 서버용 TCP 세션 베이스. 인증·방 관리·분산락 해제를 담당합니다.
    /// 게임별 세션은 이 클래스를 상속하여 OnRecv를 구현합니다.
    /// </summary>
    public abstract class GameSession : Session
    {
        /// <summary>인증 완료 후 부여되는 플레이어 ID. 인증 전에는 0입니다.</summary>
        public int SessionId { get; set; }
        /// <summary>현재 입장 중인 게임 방. 방 밖에서는 null입니다.</summary>
        public GameRoom? Room { get; set; }
        /// <summary>Redis 중복 로그인 방지 락의 고유값. 연결 종료 시 락 해제에 사용됩니다.</summary>
        public string? LoginLockValue { get; set; }

        protected override void OnConnected(EndPoint endPoint)
        {
            SessionId = 0;
            Console.WriteLine($"[GameSession] 소켓 연결됨 (인증 대기중): {endPoint}");
        }

        protected override void OnDisconnected(EndPoint endPoint)
        {
            Console.WriteLine($"[GameSession] 유저 퇴장: {endPoint}");

            GameRoom? room = Room;
            if (room != null)
                room.Push(() => room.Leave(this));

            if (SessionId > 0 && !string.IsNullOrEmpty(LoginLockValue))
            {
                string lockKey = $"player:login_lock:{SessionId}";
                _ = RedisManager.Instance.LockManager.ReleaseLockAsync(lockKey, LoginLockValue);
                Console.WriteLine($"[Redis] 유저 {SessionId} 연결 종료. 중복 로그인 락 해제 완료.");
            }
        }
    }
}
