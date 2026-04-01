using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Game
{
    public class S_BothSkillsSelected : IPacket
    {
        public PacketId PacketId => PacketId.S_BothSkillsSelected;

        public void Serialize(PacketWriter writer) { }
        public void Deserialize(PacketReader reader) { }
    }
}
