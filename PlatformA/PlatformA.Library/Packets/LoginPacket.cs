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
        public string JwtToken;


        public void Deserialize(ReadOnlySpan<byte> span)
        {
            // 🚀 payload(본문)의 시작점(0번지)부터 2바이트는 문자열(토큰)의 길이입니다.
            ushort stringLen = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(0, 2));

            // 🚀 그 다음(2번지)부터 stringLen 만큼이 진짜 JWT 토큰입니다.
            this.JwtToken = System.Text.Encoding.UTF8.GetString(span.Slice(2, stringLen));
        }
    }
}
