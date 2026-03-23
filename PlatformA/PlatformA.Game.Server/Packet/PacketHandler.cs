using PlatformA.Game.Server.Core;
using PlatformA.Game.Server.Network;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Library.Packets;

namespace PlatformA.Game.Server.Packet
{
    public class PacketHandler
    {

        // 🌟 이 명찰만 달아주면, PacketManager가 알아서 이 함수를 C_Move 패킷과 연결해줍니다!
        [PacketHandler((ushort)PacketID.C_Move)]
        public static void Handle_C_Move(GameSession session, ReadOnlySpan<byte> payload)
        {
            // 구조체 생성 및 파싱 (Zero-Allocation!)
            C_MovePacket moveReq = new C_MovePacket();
            moveReq.Deserialize(payload); // 패킷 제너레이터로 역직렬화 해제 (패킷 파싱)

            // 🚀 유저가 속한 방을 찾습니다. 방이 없으면 무시!
            GameRoom room = session.Room;
            if (room == null) return;

            room.Push(() =>
            {
                Console.WriteLine($"[C_Move] ID({session.SessionId}) 이동 -> X:{moveReq.X}, Y:{moveReq.Y}, Z:{moveReq.Z}");

                // 내가 움직였음을 남들에게 알리기.
                // 📡 1. 남들에게 뿌려줄 S_Move 패킷 만들기
                S_MovePacket moveRes = new S_MovePacket()
                {
                    PlayerId = session.SessionId,
                    X = moveReq.X,
                    Y = moveReq.Y,
                    Z = moveReq.Z
                };

                // 📡 2. 패킷 조립 (헤더 4바이트 + 본문 16바이트 = 총 20바이트)
                // 패킷 제너레이터로 패킷에 정의해둔 본문 크기 상수 활용
                ushort resSize = (ushort)(4 + S_MovePacket.Size); // 헤더 4 + 본문 16
                ushort resId = (ushort)PacketID.S_Move;

                byte[] sendBuffer = new byte[resSize];
                Span<byte> sendSpan = sendBuffer.AsSpan();

                BitConverter.TryWriteBytes(sendSpan.Slice(0, 2), resSize);
                BitConverter.TryWriteBytes(sendSpan.Slice(2, 2), resId);

                moveRes.Serialize(sendSpan.Slice(4)); // 본문 직렬화 (패킷 제너레이터 사용)

                // 세션 매니저의 전체 접속된 모든유저 (통유저) 가 아닌 같은 게임룸의 대상 유저들에게만 브로드케스팅
                room.Broadcast(sendBuffer);

            }); // 🔥 2. 방의 큐에 이동 요청 처리 작업을 던집니다.

        }



        // 로그인은 수동 제너레이팅 (문자열이라서)
        // 🚀 1. 로그인 핸들러 추가
        [PacketHandler((ushort)PacketID.C_Login)]
        public static void Handle_C_LoginAsync(GameSession session, ReadOnlySpan<byte> payload)
        {
            try
            {
                // 수동 역직렬화
                C_LoginPacket loginReq = new C_LoginPacket();
                loginReq.Deserialize(payload);

                string token = loginReq.JwtToken; // 힙에 올라갈 수 있는 일반 string 변수로 추출!
                int roomId = loginReq.RoomId; 

                _ = ProcessLoginAsync(session, token, roomId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[C_Login Critical Error] 패킷 처리 중 서버 에러 발생: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private static async Task ProcessLoginAsync(GameSession session, string jwtToken, int roomId)
        {
            // 토큰 검증 시도
            int playerId = TokenManager.ValidateTokenAndGetUserId(jwtToken);

            if (playerId > 0)
            {
                // 🚀 1. Redis 분산 락 획득 시도 (중복 로그인 방어)
                string lockKey = $"player:login_lock:{playerId} 방 번호 : {roomId}";

                // 만료시간: 1일(혹시 서버가 뻗어도 하루 뒤엔 풀림), 획득 대기: 1초, 재시도 간격: 100ms
                string lockValue = await RedisManager.Instance.LockManager.AcquireLockAsync(
                    lockKey,
                    TimeSpan.FromDays(1),
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromMilliseconds(100)
                );

                if (lockValue == null)
                {
                    // 락 획득 실패 = 이미 누가 로그인해서 락을 쥐고 있음!
                    Console.WriteLine($"[Auth Warning] 중복 로그인 차단! ID ({playerId})는 이미 접속 중입니다.");

                    // (선택) 클라이언트에게 S_Login (실패코드) 패킷을 보내주면 더 좋습니다.
                    session.Disconnect(); // 얄짤없이 소켓 끊기
                    return;
                }

                // 🚀 2. 락 획득 성공 시 (로그인 성공)
                session.LoginLockValue = lockValue; // 나중에 풀기 위해 세션에 기억
                Console.WriteLine($"[Auth] Redis 락 획득 및 인증 성공! 정식 플레이어 승급: ID ({playerId})");

                session.SessionId = playerId;
                Console.WriteLine($"[Auth] 토큰 인증 성공! 정식 플레이어 승급: ID ({playerId})");

                // 생성된 매칭 서버 방 진입.
                Core.GameRoom room = Core.GameRoomManager.Instance.FindRoom(roomId);

                // 🚀 해당 방에 유저 입장! 이제 session.Room 에 값이 생깁니다!
                room?.Push(() => room.Enter(session));

                // 만약 매칭서버가 방을 만들라고 지시했는데 아직 안 만들어졌다면 임시로 생성 (또는 방이 있을 때만 진입)
                //if (room == null)
                //{
                //    // 임시 방 생성 로직 (실제로는 Redis Pub/Sub 메시지를 받아 만들어져 있어야 함)
                //    room = new Core.GameRoom { RoomId = roomId };
                //    Core.GameRoomManager.Instance.Add(roomId, room);
                //}
            }
            else
            {
                Console.WriteLine($"[Auth] 토큰 인증 실패. 연결을 강제로 끊습니다.");
                session.Disconnect();
            }
        }
    }
}
