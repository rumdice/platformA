using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlatformA.Library.Packets
{
    // [Packets] 어트리뷰트를 달지 않습니다! (제너레이터 무시, 수동 파싱)
    public struct C_LoginPacket
    {
        public int RoomId;
        public string JwtToken;


        public void Deserialize(ReadOnlySpan<byte> span)
        {
            // 🚀 1. 본문의 시작(0번지)부터 4바이트는 무조건 RoomId 입니다.
            this.RoomId = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(span.Slice(0, 4));

            // 🚀 2. 그 다음(4번지) 2바이트는 문자열의 길이입니다.
            ushort stringLen = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(4, 2));

            // 🚀 3. 그 다음(6번지)부터 stringLen 만큼이 진짜 JWT 토큰입니다.
            this.JwtToken = System.Text.Encoding.UTF8.GetString(span.Slice(6, stringLen));
        }
    }
}
