using System.Buffers.Binary;
using System.Text.Json;
using Google.Protobuf;
using PlatformA.Game.Gomoku.Core;
using PlatformA.Game.Gomoku.Network;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Library.Game.Core;
using PlatformA.Library.Packets;
using Polly.CircuitBreaker;
using ProtoPacket = PlatformA.Library.Packets.Packet;

namespace PlatformA.Game.Gomoku.Packet
{
    public class GomokuPacketHandler
    {
        /// <summary>
        /// 로그인 패킷(CLogin)을 처리합니다.
        /// game_transfer:{userId} 키를 검증·소비하고 GomokuRoom에 입장시킵니다.
        /// </summary>
        [PacketHandler(ProtoPacket.PayloadOneofCase.CLogin)]
        public static void Handle_C_Login(GomokuSession session, ProtoPacket packet)
        {
            _ = ProcessLoginAsync(session, packet.CLogin.JwtToken);
        }

        private static async Task ProcessLoginAsync(GomokuSession session, string jwtToken)
        {
            string? lockKey = null;
            string? lockValue = null;
            try
            {
                int playerId = TokenManager.ValidateTokenAndGetUserId(jwtToken);
                if (playerId <= 0)
                {
                    await session.SendAsync(BuildResponsePacket(new ProtoPacket
                    {
                        SLogin = new SLogin { ResultCode = LoginResultCode.LoginInvalidToken, PlayerId = 0 },
                    }));
                    session.Disconnect();
                    return;
                }

                // game_transfer 티켓 검증
                string transferKey = $"{Consts.GAME_TRANSFER_KEY_PREFIX}{playerId}";
                string? transferJson;
                try
                {
                    transferJson = await RedisManager.Instance.ExecuteAsync(
                        db => db.StringGetAsync(transferKey));
                }
                catch (BrokenCircuitException)
                {
                    Console.WriteLine($"🚨 [Gomoku] 회로차단기 OPEN — 티켓 검증 불가 (User_{playerId})");
                    await session.SendAsync(BuildResponsePacket(new ProtoPacket
                    {
                        SLogin = new SLogin { ResultCode = LoginResultCode.LoginNotInQueue, PlayerId = 0 },
                    }));
                    session.Disconnect();
                    return;
                }

                if (string.IsNullOrEmpty(transferJson))
                {
                    Console.WriteLine($"🚨 [Gomoku] game_transfer 티켓 없음 — 불법 접속 시도 (User_{playerId})");
                    await session.SendAsync(BuildResponsePacket(new ProtoPacket
                    {
                        SLogin = new SLogin { ResultCode = LoginResultCode.LoginNotInQueue, PlayerId = 0 },
                    }));
                    session.Disconnect();
                    return;
                }

                // 티켓 소비 (한 번만 사용 가능)
                await RedisManager.Instance.ExecuteAsync(db => db.KeyDeleteAsync(transferKey));
                Console.WriteLine($"🎫 [Gomoku] User_{playerId} game_transfer 티켓 소비 완료");

                // roomId 파싱
                string roomId;
                using (JsonDocument doc = JsonDocument.Parse(transferJson))
                    roomId = doc.RootElement.GetProperty("roomId").GetString() ?? string.Empty;

                if (string.IsNullOrEmpty(roomId))
                {
                    Console.WriteLine($"[Gomoku] roomId 파싱 실패 (User_{playerId})");
                    session.Disconnect();
                    return;
                }

                // 중복 로그인 방지 락
                lockKey = $"{Consts.LOGIN_LOCK_KEY_PREFIX}{playerId}";
                lockValue = await RedisManager.Instance.LockManager.AcquireLockAsync(
                    lockKey,
                    TimeSpan.FromDays(1),
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromMilliseconds(100));

                if (lockValue == null)
                {
                    Console.WriteLine($"[Gomoku] 중복 로그인 차단 (User_{playerId})");
                    await session.SendAsync(BuildResponsePacket(new ProtoPacket
                    {
                        SLogin = new SLogin { ResultCode = LoginResultCode.LoginDuplicate, PlayerId = 0 },
                    }));
                    session.Disconnect();
                    return;
                }

                session.LoginLockValue = lockValue;
                session.SessionId = playerId;
                Console.WriteLine($"[Gomoku] 인증 성공 — User_{playerId}, room={roomId}");

                GomokuRoom room = GomokuRoomManager.Instance.GetOrCreate(roomId);
                room.Push(() =>
                {
                    room.Enter(session);
                    _ = session.SendAsync(BuildResponsePacket(new ProtoPacket
                    {
                        SLogin = new SLogin { ResultCode = LoginResultCode.LoginSuccess, PlayerId = playerId },
                    }));
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Gomoku] ProcessLoginAsync 오류: {ex.Message}");
                if (lockValue != null && lockKey != null)
                    await RedisManager.Instance.LockManager.ReleaseLockAsync(lockKey, lockValue);
                session.Disconnect();
            }
        }

        [PacketHandler(ProtoPacket.PayloadOneofCase.CPlaceStone)]
        public static void Handle_C_PlaceStone(GomokuSession session, ProtoPacket packet)
        {
            CPlaceStone req = packet.CPlaceStone;
            if (session.Room is not GomokuRoom room)
                return;

            room.Push(() => room.HandlePlaceStone(session, req.X, req.Y));
        }

        private static byte[] BuildResponsePacket(ProtoPacket envelope)
        {
            byte[] envelopeBytes = envelope.ToByteArray();
            ushort size = (ushort)(2 + envelopeBytes.Length);
            byte[] buf = new byte[size];
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), size);
            envelopeBytes.CopyTo(buf, 2);
            return buf;
        }
    }
}
