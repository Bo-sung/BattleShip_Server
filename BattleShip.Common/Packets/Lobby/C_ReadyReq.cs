using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Lobby
{
    public class C_ReadyReq : IPacket
    {
        public PacketId PacketId => PacketId.C_ReadyReq;

        public void Serialize(PacketWriter writer) { }
        public void Deserialize(PacketReader reader) { }
    }
}
