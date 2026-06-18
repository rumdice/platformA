using System.Buffers;
using Google.Protobuf;
using PlatformA.Game.Gomoku.Core;
using PlatformA.Library.Core;
using PlatformA.Library.Game.Core;
using PlatformA.Library.Game.Network;
using PlatformA.Library.Packets;
using ProtoPacket = PlatformA.Library.Packets.Packet;

namespace PlatformA.Game.Gomoku.Network
{
    /// <summary>
    /// Gomoku 전용 TCP 세션. Protobuf 패킷을 수신하여 GomokuRoom으로 디스패치합니다.
    /// </summary>
    public class GomokuSession : GameSession
    {
        protected override void OnRecv(ReadOnlySequence<byte> packet)
        {
            ReadOnlySpan<byte> span = packet.IsSingleSegment ? packet.FirstSpan : packet.ToArray().AsSpan();
            ReadOnlySpan<byte> envelopeBytes = span.Slice(2);
            try
            {
                ProtoPacket envelope = ProtoPacket.Parser.ParseFrom(envelopeBytes);
                PacketManager<GomokuSession>.Instance.HandlePacket(this, envelope);
            }
            catch (InvalidProtocolBufferException ex)
            {
                Console.WriteLine($"[GomokuSession] 잘못된 패킷: {ex.Message}");
                Disconnect();
            }
        }

        protected override void OnDisconnected(System.Net.EndPoint endPoint)
        {
            GameRoom? room = Room;
            if (room is GomokuRoom gomokuRoom)
                gomokuRoom.Push(() => gomokuRoom.HandleDisconnect(this));

            base.OnDisconnected(endPoint);
        }
    }
}
