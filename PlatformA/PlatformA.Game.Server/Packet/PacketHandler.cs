using Polly.CircuitBreaker;
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

        // S_Login 패킷 조립 헬퍼
        private static byte[] BuildS_LoginPacket(int resultCode, int playerId)
        {
            ushort totalSize = (ushort)(4 + S_LoginPacket.Size); // header(4) + body(8) = 12
            byte[] buffer = new byte[totalSize];
            Span<byte> span = buffer.AsSpan();
            BitConverter.TryWriteBytes(span.Slice(0, 2), totalSize);
            BitConverter.TryWriteBytes(span.Slice(2, 2), (ushort)PacketID.S_Login);
            new S_LoginPacket { ResultCode = resultCode, PlayerId = playerId }.Serialize(span.Slice(4));
            return buffer;
        }

        private static async Task ProcessLoginAsync(GameSession session, string jwtToken, int roomId)
        {
            // 1. JWT 토큰 검증
            int playerId = TokenManager.ValidateTokenAndGetUserId(jwtToken);

            if (playerId <= 0)
            {
                Console.WriteLine($"[GameServer] JWT 토큰 인증 실패. 연결을 강제로 끊습니다.");
                await session.SendAsync(BuildS_LoginPacket(S_LoginPacket.ResultInvalidToken, 0));
                session.Disconnect();
                return;
            }

            // 2. 대기열(Active) 문지기 검증
            string activeKey = $"{Consts.ACTIVE_USER_KEY_PREFIX}{playerId}";
            bool isActive;
            try
            {
                isActive = await RedisManager.Instance.ExecuteAsync(db => db.KeyExistsAsync(activeKey));
            }
            catch (BrokenCircuitException)
            {
                Console.WriteLine($"🚨 [Redis 장애] 회로차단기 OPEN — 입장권 검증 불가. 접속 거부 (User_{playerId})");
                await session.SendAsync(BuildS_LoginPacket(S_LoginPacket.ResultNotInQueue, 0));
                session.Disconnect();
                return;
            }

            if (!isActive)
            {
                Console.WriteLine($"🚨 [보안 경고] 대기열을 거치지 않은 불법 접속 시도! (User_{playerId})");
                await session.SendAsync(BuildS_LoginPacket(S_LoginPacket.ResultNotInQueue, 0));
                session.Disconnect();
                return;
            }

            // 3. 입장권 회수
            await RedisManager.Instance.ExecuteAsync(db => db.KeyDeleteAsync(activeKey));
            Console.WriteLine($"🎫 [티켓 확인] User_{playerId} 님의 입장권을 회수했습니다.");

            // 4. 중복 로그인 방어 (분산 락)
            string lockKey = $"player:login_lock:{playerId}";
            string lockValue = await RedisManager.Instance.LockManager.AcquireLockAsync(
                lockKey,
                TimeSpan.FromDays(1),
                TimeSpan.FromSeconds(1),
                TimeSpan.FromMilliseconds(100)
            );

            if (lockValue == null)
            {
                Console.WriteLine($"[GameServer Warning] 중복 로그인 차단! ID ({playerId})는 이미 접속 중입니다.");
                await session.SendAsync(BuildS_LoginPacket(S_LoginPacket.ResultDuplicate, 0));
                session.Disconnect();
                return;
            }

            // 5. 방 찾기
            int targetRoomId = roomId > 0 ? roomId : 1;
            Core.GameRoom room = Core.GameRoomManager.Instance.FindRoom(targetRoomId);

            if (room == null)
            {
                Console.WriteLine($"[GameServer Warning] 방({targetRoomId})이 아직 생성되지 않았습니다. (User_{playerId})");
                await session.SendAsync(BuildS_LoginPacket(S_LoginPacket.ResultRoomNotFound, 0));
                await RedisManager.Instance.LockManager.ReleaseLockAsync(lockKey, lockValue);
                session.Disconnect();
                return;
            }

            // 6. 로그인 성공 — 입장 후 S_Login 전송 (레이스 컨디션 방지)
            session.LoginLockValue = lockValue;
            session.SessionId = playerId;
            Console.WriteLine($"[GameServer] 인증 성공! 정식 플레이어 승급: ID ({playerId})");

            room.Push(() =>
            {
                room.Enter(session);
                _ = session.SendAsync(BuildS_LoginPacket(S_LoginPacket.ResultSuccess, playerId));
            });
        }
    }
}
