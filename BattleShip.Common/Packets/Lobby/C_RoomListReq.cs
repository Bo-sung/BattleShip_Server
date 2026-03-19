using BattleShip.Common.Network;
using System.Collections.Generic;

namespace BattleShip.Common.Packets.Lobby
{
    public class C_RoomListReq : IPacket
    {
        public PacketId PacketId => PacketId.C_RoomListReq;

        public void Serialize(PacketWriter writer) { }
        public void Deserialize(PacketReader reader) { }
    }
}
