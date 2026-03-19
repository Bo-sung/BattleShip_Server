using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Game
{
    public class S_PlacementDone : IPacket
    {
        public PacketId PacketId => PacketId.S_PlacementDone;

        public void Serialize(PacketWriter writer) { }
        public void Deserialize(PacketReader reader) { }
    }
}
