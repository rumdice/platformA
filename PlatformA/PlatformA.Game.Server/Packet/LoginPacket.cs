using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlatformA.Game.Server.Packet
{
    // [Packet] 어트리뷰트를 달지 않습니다! (제너레이터 무시, 수동 파싱)
    public struct C_LoginPacket
    {
        public string JwtToken;

        public void Deserialize(ReadOnlySpan<byte> span)
        {
            // 규칙: 앞의 2바이트는 문자열의 길이, 그 뒤는 문자열 데이터
            ushort stringLen = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(0, 2));
            this.JwtToken = System.Text.Encoding.UTF8.GetString(span.Slice(2, stringLen));
        }
    }
}
