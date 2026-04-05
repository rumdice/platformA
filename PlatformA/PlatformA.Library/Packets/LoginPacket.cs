using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlatformA.Library.Packets
{
    // Server → Client 로그인 결과 패킷
    // payload 포맷: ResultCode(4) + PlayerId(4) = 8 bytes
    public struct S_LoginPacket
    {
        public const int ResultSuccess      = 0; // 로그인 성공
        public const int ResultInvalidToken = 1; // JWT 유효하지 않음
        public const int ResultNotInQueue   = 2; // 대기열 미통과 (Active 키 없음)
        public const int ResultDuplicate    = 3; // 중복 로그인
        public const int ResultRoomNotFound = 4; // 방 없음

        public int ResultCode;
        public int PlayerId;

        public const int Size = 8;

        public void Serialize(Span<byte> span)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(span.Slice(0, 4), ResultCode);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(span.Slice(4, 4), PlayerId);
        }

        public void Deserialize(ReadOnlySpan<byte> span)
        {
            ResultCode = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(span.Slice(0, 4));
            PlayerId   = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(span.Slice(4, 4));
        }
    }

    // ── 방 이동 패킷 ────────────────────────────────────────────────────

    // Client -> Server: 매칭 성사 후 로비에서 게임방으로 이동 요청
    // payload 포맷: RoomId(4)
    public struct C_EnterRoomPacket
    {
        public int RoomId;
        public const int Size = 4;

        public void Deserialize(ReadOnlySpan<byte> span)
        {
            RoomId = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(span.Slice(0, 4));
        }
    }

    // Server -> Client: 방 이동 결과
    // payload 포맷: ResultCode(4) + RoomId(4)
    public struct S_EnterRoomPacket
    {
        public const int ResultSuccess      = 0;
        public const int ResultRoomNotFound = 1;

        public int ResultCode;
        public int RoomId;

        public const int Size = 8;

        public void Serialize(Span<byte> span)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(span.Slice(0, 4), ResultCode);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(span.Slice(4, 4), RoomId);
        }

        public void Deserialize(ReadOnlySpan<byte> span)
        {
            ResultCode = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(span.Slice(0, 4));
            RoomId     = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(span.Slice(4, 4));
        }
    }

    // ── 로그인 패킷 ─────────────────────────────────────────────────────

    // [Packets] 어트리뷰트를 달지 않습니다! (제너레이터 무시, 수동 파싱)
    // payload 포맷: RoomId(4) + stringLen(2) + token(N)
    // RoomId == 1 : 광장(plaza), 항상 열려있는 기본 방
    // RoomId > 1  : 매칭 서버가 발급한 실제 게임 방
    public struct C_LoginPacket
    {
        public int RoomId;
        public string JwtToken;

        public void Deserialize(ReadOnlySpan<byte> span)
        {
            // 0~3번지: RoomId (int32)
            this.RoomId = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(span.Slice(0, 4));

            // 4~5번지: JWT 토큰 문자열 길이 (ushort)
            ushort stringLen = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(4, 2));

            // 6번지~: JWT 토큰 문자열 본문
            this.JwtToken = System.Text.Encoding.UTF8.GetString(span.Slice(6, stringLen));
        }
    }
}
