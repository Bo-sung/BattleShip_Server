using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Game
{
    public class S_SkillShieldRes : IPacket
    {
        public PacketId PacketId => PacketId.S_SkillShieldRes;

        public bool Success { get; set; }
        public byte ShipType { get; set; }
        public byte Duration { get; set; }  // 2 = 2턴 유지
        public byte Mana { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Success);
            writer.Write(ShipType);
            writer.Write(Duration);
            writer.Write(Mana);
        }

        public void Deserialize(PacketReader reader)
        {
            Success = reader.ReadBool();
            ShipType = reader.ReadByte();
            Duration = reader.ReadByte();
            Mana = reader.ReadByte();
        }
    }
}
