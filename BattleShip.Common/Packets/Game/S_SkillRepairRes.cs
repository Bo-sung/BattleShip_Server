using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Game
{
    public class S_SkillRepairRes : IPacket
    {
        public PacketId PacketId => PacketId.S_SkillRepairRes;

        public bool Success { get; set; }
        public byte ShipType { get; set; }
        public byte Mana { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Success);
            writer.Write(ShipType);
            writer.Write(Mana);
        }

        public void Deserialize(PacketReader reader)
        {
            Success = reader.ReadBool();
            ShipType = reader.ReadByte();
            Mana = reader.ReadByte();
        }
    }
}
