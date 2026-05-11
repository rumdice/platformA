using System.Buffers.Binary;
using System.Net.Sockets;
using Google.Protobuf;
using PlatformA.Library.Packets;

namespace PlatformA.Game.DummyClient.Scenarios
{
    internal static class PacketHelper
    {
        // 송신: Packet envelope → [ushort size (2B)][Packet bytes]
        internal static byte[] BuildPacket(Packet envelope)
        {
            byte[] envelopeBytes = envelope.ToByteArray();
            ushort size = (ushort)(2 + envelopeBytes.Length);
            byte[] buf = new byte[size];
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), size);
            envelopeBytes.CopyTo(buf, 2);
            return buf;
        }

        // 수신: size 헤더를 실제로 사용해 정확한 바이트 수를 읽음 (TCP 단편화 대응)
        // 반환값 null = 연결 종료
        internal static async Task<byte[]?> ReceiveFrameAsync(Socket socket, CancellationToken ct = default)
        {
            byte[] sizeBuf = new byte[2];
            int totalRead = 0;
            while (totalRead < 2)
            {
                int n = await socket.ReceiveAsync(sizeBuf.AsMemory(totalRead), SocketFlags.None, ct);
                if (n == 0)
                    return null;
                totalRead += n;
            }

            ushort frameSize = BinaryPrimitives.ReadUInt16LittleEndian(sizeBuf);
            if (frameSize < 2)
                return null;

            byte[] frame = new byte[frameSize];
            sizeBuf.CopyTo(frame, 0);
            totalRead = 2;
            while (totalRead < frameSize)
            {
                int n = await socket.ReceiveAsync(frame.AsMemory(totalRead), SocketFlags.None, ct);
                if (n == 0)
                    return null;
                totalRead += n;
            }

            return frame;
        }

        // frame[2..] 을 Packet envelope 으로 파싱
        internal static Packet ParseEnvelope(byte[] frame)
            => Packet.Parser.ParseFrom(frame, 2, frame.Length - 2);
    }
}
