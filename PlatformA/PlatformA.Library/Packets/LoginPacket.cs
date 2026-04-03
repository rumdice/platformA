using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlatformA.Library.Packets
{
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
