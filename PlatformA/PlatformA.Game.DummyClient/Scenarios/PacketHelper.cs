using System.Buffers.Binary;
using Google.Protobuf;
using PlatformA.Library.Packets;

namespace PlatformA.Game.DummyClient.Scenarios
{
    internal static class PacketHelper
    {
        internal static byte[] BuildPacket(PacketID id, IMessage message)
        {
            byte[] payload = message.ToByteArray();
            ushort size = (ushort)(4 + payload.Length);
            byte[] buf = new byte[size];
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), size);
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2, 2), (ushort)id);
            payload.CopyTo(buf, 4);
            return buf;
        }
    }
}
