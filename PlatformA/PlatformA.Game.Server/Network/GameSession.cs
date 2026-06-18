using System.Buffers;
using Google.Protobuf;
using PlatformA.Game.Server.Packet;
using PlatformA.Library.Core;
using PlatformA.Library.Game.Network;
using PlatformA.Library.Packets;
using ProtoPacket = PlatformA.Library.Packets.Packet;

namespace PlatformA.Game.Server.Network
{
    /// <summary>
    /// PlatformA.Game.Server 전용 세션. Protobuf 패킷을 수신하여 PacketHandler로 dispatch합니다.
    /// </summary>
    public class GameSession : PlatformA.Library.Game.Network.GameSession
    {
        protected override void OnRecv(ReadOnlySequence<byte> packet)
        {
            ReadOnlySpan<byte> span = packet.IsSingleSegment ? packet.FirstSpan : packet.ToArray().AsSpan();

            ReadOnlySpan<byte> envelopeBytes = span.Slice(2);
            try
            {
                ProtoPacket envelope = ProtoPacket.Parser.ParseFrom(envelopeBytes);
                PacketManager<GameSession>.Instance.HandlePacket(this, envelope);
            }
            catch (InvalidProtocolBufferException ex)
            {
                Console.WriteLine($"[OnRecv] 잘못된 패킷: {ex.Message}");
                Disconnect();
            }
        }
    }
}
