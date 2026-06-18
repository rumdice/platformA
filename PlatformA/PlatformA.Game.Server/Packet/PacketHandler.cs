using System.Buffers.Binary;
using Google.Protobuf;
using PlatformA.Library.Game.Core;
using PlatformA.Game.Server.Network;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Library.Packets;
using Polly.CircuitBreaker;
using ProtoPacket = PlatformA.Library.Packets.Packet;

namespace PlatformA.Game.Server.Packet
{
    /// <summary>
    /// 클라이언트로부터 수신한 Protobuf 패킷을 처리하는 핸들러 모음.
    /// 각 메서드는 <see cref="PacketHandlerAttribute"/>로 등록되며
    /// 게임 상태 변경은 반드시 <see cref="GameRoom.Push"/>를 통해 직렬화합니다.
    /// </summary>
    public class PacketHandler
    {
        /// <summary>클라이언트 이동 패킷(CMove)을 처리하고 방 전체에 SMove를 브로드캐스트합니다.</summary>
        [PacketHandler(ProtoPacket.PayloadOneofCase.CMove)]
        public static void Handle_C_Move(GameSession session, ProtoPacket packet)
        {
            CMove moveReq = packet.CMove;

            GameRoom room = session.Room;
            if (room == null)
                return;

            room.Push(() =>
            {
                Console.WriteLine($"[C_Move] ID({session.SessionId}) 이동 -> X:{moveReq.X}, Y:{moveReq.Y}, Z:{moveReq.Z}");

                room.Broadcast(BuildResponsePacket(new ProtoPacket
                {
                    SMove = new SMove
                    {
                        PlayerId = session.SessionId,
                        X = moveReq.X,
                        Y = moveReq.Y,
                        Z = moveReq.Z,
                    },
                }));
            });
        }

        /// <summary>방 입장 패킷(CEnterRoom)을 처리하고 입장 결과(SEnterRoom)를 응답합니다.</summary>
        [PacketHandler(ProtoPacket.PayloadOneofCase.CEnterRoom)]
        public static void Handle_C_EnterRoom(GameSession session, ProtoPacket packet)
        {
            if (session.SessionId <= 0)
                return;

            ProcessEnterRoom(session, packet.CEnterRoom.RoomId);
        }

        /// <summary>로그인 패킷(CLogin)을 처리합니다. JWT 검증·중복 로그인 방지·방 입장을 순서대로 수행합니다.</summary>
        [PacketHandler(ProtoPacket.PayloadOneofCase.CLogin)]
        public static void Handle_C_LoginAsync(GameSession session, ProtoPacket packet)
        {
            try
            {
                CLogin loginReq = packet.CLogin;
                _ = ProcessLoginAsync(session, loginReq.JwtToken, loginReq.RoomId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[C_Login Critical Error] 패킷 처리 중 서버 에러 발생: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private static void ProcessEnterRoom(GameSession session, int roomId)
        {
            GameRoom newRoom = GameRoomManager.Instance.FindRoom(roomId);
            if (newRoom == null)
            {
                Console.WriteLine($"[C_EnterRoom] 방({roomId})이 존재하지 않습니다. (User_{session.SessionId})");
                _ = session.SendAsync(BuildResponsePacket(new ProtoPacket
                {
                    SEnterRoom = new SEnterRoom
                    {
                        ResultCode = EnterRoomResultCode.EnterRoomNotFound,
                        RoomId = roomId,
                    },
                }));
                return;
            }

            GameRoom currentRoom = session.Room;
            if (currentRoom != null)
            {
                currentRoom.Push(() =>
                {
                    currentRoom.Leave(session);
                    newRoom.Push(() =>
                    {
                        newRoom.Enter(session);
                        _ = session.SendAsync(BuildResponsePacket(new ProtoPacket
                        {
                            SEnterRoom = new SEnterRoom
                            {
                                ResultCode = EnterRoomResultCode.EnterRoomSuccess,
                                RoomId = roomId,
                            },
                        }));
                    });
                });
            }
            else
            {
                newRoom.Push(() =>
                {
                    newRoom.Enter(session);
                    _ = session.SendAsync(BuildResponsePacket(new ProtoPacket
                    {
                        SEnterRoom = new SEnterRoom
                        {
                            ResultCode = EnterRoomResultCode.EnterRoomSuccess,
                            RoomId = roomId,
                        },
                    }));
                });
            }
        }

        private static async Task ProcessLoginAsync(GameSession session, string jwtToken, int roomId)
        {
            string? lockKey = null;
            string? lockValue = null;

            try
            {
                int playerId = TokenManager.ValidateTokenAndGetUserId(jwtToken);

                if (playerId <= 0)
                {
                    Console.WriteLine($"[GameServer] JWT 토큰 인증 실패. 연결을 강제로 끊습니다.");
                    await session.SendAsync(BuildResponsePacket(new ProtoPacket
                    {
                        SLogin = new SLogin { ResultCode = LoginResultCode.LoginInvalidToken, PlayerId = 0 },
                    }));
                    session.Disconnect();
                    return;
                }

                string activeKey = $"{Consts.ACTIVE_USER_KEY_PREFIX}{playerId}";
                bool isActive;
                try
                {
                    isActive = await RedisManager.Instance.ExecuteAsync(db => db.KeyExistsAsync(activeKey));
                }
                catch (BrokenCircuitException)
                {
                    Console.WriteLine($"🚨 [Redis 장애] 회로차단기 OPEN — 입장권 검증 불가. 접속 거부 (User_{playerId})");
                    await session.SendAsync(BuildResponsePacket(new ProtoPacket
                    {
                        SLogin = new SLogin { ResultCode = LoginResultCode.LoginNotInQueue, PlayerId = 0 },
                    }));
                    session.Disconnect();
                    return;
                }

                if (!isActive)
                {
                    Console.WriteLine($"🚨 [보안 경고] 대기열을 거치지 않은 불법 접속 시도! (User_{playerId})");
                    await session.SendAsync(BuildResponsePacket(new ProtoPacket
                    {
                        SLogin = new SLogin { ResultCode = LoginResultCode.LoginNotInQueue, PlayerId = 0 },
                    }));
                    session.Disconnect();
                    return;
                }

                await RedisManager.Instance.ExecuteAsync(db => db.KeyDeleteAsync(activeKey));
                Console.WriteLine($"🎫 [티켓 확인] User_{playerId} 님의 입장권을 회수했습니다.");

                lockKey = $"{Consts.LOGIN_LOCK_KEY_PREFIX}{playerId}";
                lockValue = await RedisManager.Instance.LockManager.AcquireLockAsync(
                    lockKey,
                    TimeSpan.FromDays(1),
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromMilliseconds(100));

                if (lockValue == null)
                {
                    Console.WriteLine($"[GameServer Warning] 중복 로그인 차단! ID ({playerId})는 이미 접속 중입니다.");
                    await session.SendAsync(BuildResponsePacket(new ProtoPacket
                    {
                        SLogin = new SLogin { ResultCode = LoginResultCode.LoginDuplicate, PlayerId = 0 },
                    }));
                    session.Disconnect();
                    return;
                }

                int targetRoomId = roomId > 0 ? roomId : 1;
                GameRoom? room = GameRoomManager.Instance.FindRoom(targetRoomId);

                if (room == null)
                {
                    Console.WriteLine($"[GameServer Warning] 방({targetRoomId})이 아직 생성되지 않았습니다. (User_{playerId})");
                    await session.SendAsync(BuildResponsePacket(new ProtoPacket
                    {
                        SLogin = new SLogin { ResultCode = LoginResultCode.LoginRoomNotFound, PlayerId = 0 },
                    }));
                    await RedisManager.Instance.LockManager.ReleaseLockAsync(lockKey, lockValue);
                    session.Disconnect();
                    return;
                }

                session.LoginLockValue = lockValue;
                session.SessionId = playerId;
                Console.WriteLine($"[GameServer] 인증 성공! 정식 플레이어 승급: ID ({playerId})");

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
                Console.WriteLine($"ProcessLoginAsync Error {ex.Message}");

                if (lockValue != null)
                    await RedisManager.Instance.LockManager.ReleaseLockAsync(lockKey, lockValue);

                session.Disconnect();
            }
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
