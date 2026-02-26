using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

/// 게임서버에서 캐릭터의 이동에 관련된 패킷을 정의한다.
///게임 패킷은 보통 다음과 같은 형태
///[헤더] 사이즈  (2 바이트): 이 패킷의 전체 길이 (어제 파이프라인에서 자를 때 썼던 그것)
///[헤더] 패킷 ID (2 바이트): 이 패킷이 "이동"인지, "공격"인지 구분하는 번호
///[본문] 데이터: 실제 내용(좌표 등)

namespace PlatformA.Game.Server.Packet
{
    // 패킷 종류를 구분하는 Enum
    public enum PacketID : ushort
    {
        C_Move = 1, // Client -> Server 이동 요청
        S_Move = 2  // Server -> Client 이동 결과 (브로드캐스트용)
    }

    // Client -> Server
    // 🔥 게임 로직에서 쓸 이동 패킷 구조체 (class가 아니라 struct입니다! GC 할당 없음)
    // 내가 이만큼 움직였어!
    public struct C_MovePacket
    {
        public float X;
        public float Y;
        public float Z;

        // 본문 크기 (float 3개 = 12바이트)
        public const ushort Size = 12;

        // 1. [역직렬화] 바이트 배열 -> 구조체 변환 (Reader)
        public void Deserialize(ReadOnlySpan<byte> span)
        {
            // BinaryPrimitives를 쓰면 메모리 복사 없이, Endian 이슈까지 안전하게 파싱합니다.
            X = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(0, 4));
            Y = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(4, 4));
            Z = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(8, 4));
        }

        // 2. [직렬화] 구조체 -> 바이트 배열 변환 (Writer)
        public void Serialize(Span<byte> span)
        {
            BinaryPrimitives.WriteSingleLittleEndian(span.Slice(0, 4), X);
            BinaryPrimitives.WriteSingleLittleEndian(span.Slice(4, 4), Y);
            BinaryPrimitives.WriteSingleLittleEndian(span.Slice(8, 4), Z);
        }
    }

    // Server -> Client
    // 이동에 관련한 패킷을 서버가 클라로 던진다.
    public struct S_MovePacket
    {
        public int PlayerId; // "누가" 움직였는가? (4바이트)
        public float X;      // (4바이트)
        public float Y;      // (4바이트)
        public float Z;      // (4바이트)

        public const ushort Size = 16; // 4 + 4 + 4 + 4 = 16바이트 (본문 크기)

        // 직렬화: 구조체 -> 바이트 배열 (브로드캐스팅 용도)
        public void Serialize(Span<byte> span)
        {
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(0, 4), PlayerId);
            BinaryPrimitives.WriteSingleLittleEndian(span.Slice(4, 4), X);
            BinaryPrimitives.WriteSingleLittleEndian(span.Slice(8, 4), Y);
            BinaryPrimitives.WriteSingleLittleEndian(span.Slice(12, 4), Z);
        }
    }
}
