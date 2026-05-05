using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using PlatformA.Game.Server.Core;
using PlatformA.Library.Core;
using PlatformA.Library.Network;
using PlatformA.Library.Packets;

namespace PlatformA.Game.Server.Network
{
    public class GameSession : Session
    {
        // 임시로 부여할 플레이어 ID (실제로는 로그인할 때 DB에서 가져오거나 자동 발급)
        public int SessionId { get; set; }
        public GameRoom? Room { get; set; } // 🚀 내가 속한 방 객체 기억하기
        public string? LoginLockValue { get; set; } // 내가 획득한 분산락 고유값

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
            // ReadOnlySequence가 여러 조각으로 나뉘어 있을 수 있으니
            // 안전하게 연속된 Span으로 뽑아냅니다. (보통 단일 패킷은 한 조각에 들어있음)
            ReadOnlySpan<byte> span = packet.IsSingleSegment ? packet.FirstSpan : packet.ToArray().AsSpan();

            // 1. 헤더 파싱 (사이즈 2바이트 + 패킷 ID 2바이트)
            // 사이즈는 이미 파이프라인에서 검증하고 잘라서 넘겨주었으니 건너뛰고, 패킷 ID만 읽습니다.
            //ushort packetId = BitConverter.ToUInt16(span.Slice(2, 2));

            // review : 엔디안 방식 명시하기.
            ushort packetId = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(2));

            // 본문 잘라내기 (파싱)
            // 본문은 앞의 헤더(4바이트: 사이즈 2 + ID 2)를 제외한 부분
            ReadOnlySpan<byte> payload = span.Slice(4);

            // 2. 패킷 종류에 따른 라우팅
            // 🔥 2. 거대한 switch-case 제거! PacketManager에게 처리를 맡깁니다.
            PacketManager<GameSession>.Instance.HandlePacket(this, packetId, payload);
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
