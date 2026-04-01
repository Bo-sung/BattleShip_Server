using BattleShip.Common.Network;
using System.Collections.Generic;

namespace BattleShip.Common.Packets.Game
{
    public class S_SkillAttackRes : IPacket
    {
        public PacketId PacketId => PacketId.S_SkillAttackRes;

        public byte SkillType { get; set; }
        public List<CellResult> Cells { get; set; } = new List<CellResult>();
        public byte Mana { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(SkillType);
            writer.Write((byte)Cells.Count);
            foreach (var cell in Cells)
                cell.Serialize(writer);
            writer.Write(Mana);
        }

        public void Deserialize(PacketReader reader)
        {
            SkillType = reader.ReadByte();
            byte count = reader.ReadByte();
            for (int i = 0; i < count; i++)
                Cells.Add(CellResult.Deserialize(reader));
            Mana = reader.ReadByte();
        }
    }
}
