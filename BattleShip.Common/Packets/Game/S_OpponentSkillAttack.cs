using BattleShip.Common.Network;
using System.Collections.Generic;

namespace BattleShip.Common.Packets.Game
{
    public class S_OpponentSkillAttack : IPacket
    {
        public PacketId PacketId => PacketId.S_OpponentSkillAttack;

        public byte SkillType { get; set; }
        public List<CellResult> Cells { get; set; } = new List<CellResult>();

        public void Serialize(PacketWriter writer)
        {
            writer.Write(SkillType);
            writer.Write((byte)Cells.Count);
            foreach (var cell in Cells)
                cell.Serialize(writer);
        }

        public void Deserialize(PacketReader reader)
        {
            SkillType = reader.ReadByte();
            byte count = reader.ReadByte();
            for (int i = 0; i < count; i++)
                Cells.Add(CellResult.Deserialize(reader));
        }
    }
}
