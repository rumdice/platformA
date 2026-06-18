using PlatformA.Game.Gomoku.Core;
using PlatformA.Game.Gomoku.Network;
using PlatformA.Library.Core;
using PlatformA.Library.Packets;
using ProtoPacket = PlatformA.Library.Packets.Packet;

namespace PlatformA.Game.Gomoku.Packet
{
    public class GomokuPacketHandler
    {
        [PacketHandler(ProtoPacket.PayloadOneofCase.CPlaceStone)]
        public static void Handle_C_PlaceStone(GomokuSession session, ProtoPacket packet)
        {
            CPlaceStone req = packet.CPlaceStone;
            if (session.Room is not GomokuRoom room)
                return;

            room.Push(() => room.HandlePlaceStone(session, req.X, req.Y));
        }
    }
}
