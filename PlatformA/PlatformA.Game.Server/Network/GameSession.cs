using System.Buffers;
using System.Net;
using Google.Protobuf;
using PlatformA.Game.Server.Core;
using PlatformA.Library.Core;
using PlatformA.Library.Network;
using PlatformA.Library.Packets;
using ProtoPacket = PlatformA.Library.Packets.Packet;

namespace PlatformA.Game.Server.Network
{
    /// <summary>
    /// 클라이언트 연결 단위. <see cref="Session"/> 기반 TCP 파이프라인 위에서
    /// 인증·방 입장·분산락 해제를 담당합니다.
    /// </summary>
    public class GameSession : Session
    {
        /// <summary>인증 완료 후 부여되는 플레이어 ID. 인증 전에는 0입니다.</summary>
        public int SessionId { get; set; }
        /// <summary>현재 입장 중인 게임 방. 방 밖에서는 null입니다.</summary>
        public GameRoom? Room { get; set; }
        /// <summary>Redis 중복 로그인 방지 락의 고유값. 연결 종료 시 락 해제에 사용됩니다.</summary>
        public string? LoginLockValue { get; set; }

        protected override void OnConnected(EndPoint endPoint)
        {
            // 임시 ID 발급 (해시코드 등 사용)
            //SessionId = endPoint.GetHashCode(); // 개선 포인트 : 입장할때마다 ~님이 바뀜.
            //Console.WriteLine($"[GameSession] 유저 입장: {endPoint} (ID: {SessionId})");

            // Auth 연동. 로그인 절차로 교체
            SessionId = 0;
            Console.WriteLine($"[GameSession] 소켓 연결됨 (인증 대기중): {endPoint}");

            // 🔥 1. SessionManager 대신 방(GameRoom)의 큐에 입장 작업을 던집니다.
            //GameRoom.GlobalRoom.Push(() => GameRoom.GlobalRoom.Enter(this));

            // 🚀 테스트용: 무조건 1번 방을 찾아서 들어갑니다. (나중에는 매칭 서버가 정해준 방으로 갑니다)
            //GameRoom room = GameRoomManager.Instance.FindRoom(1);
            //if (room != null)
            //{
            //    room.Push(() => room.Enter(this));
            //}
        }

        /// <summary>
        /// 문자열을 버리고 정의된 구조체 바이너리 패킷으로 수신한다.
        /// </summary>
        /// <param name="packet"></param>
        protected override void OnRecv(ReadOnlySequence<byte> packet)
        {
            ReadOnlySpan<byte> span = packet.IsSingleSegment ? packet.FirstSpan : packet.ToArray().AsSpan();

            // size 2바이트 건너뛰고 Packet envelope 파싱 (offset 2부터 끝까지)
            ReadOnlySpan<byte> envelopeBytes = span.Slice(2);
            try
            {
                ProtoPacket envelope = ProtoPacket.Parser.ParseFrom(envelopeBytes);
                PacketManager<GameSession>.Instance.HandlePacket(this, envelope);
            }
            catch (InvalidProtocolBufferException ex)
            {
                Console.WriteLine($"[OnRecv] 잘못된 패킷: {ex.Message}");
                Disconnect();
            }
        }


        protected override void OnDisconnected(EndPoint endPoint)
        {
            Console.WriteLine($"[GameSession] 유저 퇴장: {endPoint}");

            // 🔥 3. 퇴장 처리도 방(GameRoom)의 큐를 통해 안전하게 진행합니다. (방에서 세션 제거)
            //GameRoom.GlobalRoom.Push(() => GameRoom.GlobalRoom.Leave(this));

            // 🚀 내가 속해있던 방에서 안전하게 퇴장합니다.
            GameRoom room = Room;
            if (room != null)
            {
                room.Push(() => room.Leave(this));
            }

            // 연결이 끊어질 때 Redis 락 해제
            if (SessionId > 0 && !string.IsNullOrEmpty(LoginLockValue))
            {
                string lockKey = $"player:login_lock:{SessionId}";
                // 비동기로 던져두고 잊기 (Fire and Forget)
                _ = RedisManager.Instance.LockManager.ReleaseLockAsync(lockKey, LoginLockValue);
                Console.WriteLine($"[Redis] 유저 {SessionId} 연결 종료. 중복 로그인 락 해제 완료.");
            }
        }
    }
}
