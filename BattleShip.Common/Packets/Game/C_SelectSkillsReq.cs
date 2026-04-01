using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Game
{
    public class C_SelectSkillsReq : IPacket
    {
        public PacketId PacketId => PacketId.C_SelectSkillsReq;

        public byte Skill1 { get; set; }
        public byte Skill2 { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Skill1);
            writer.Write(Skill2);
        }

        public void Deserialize(PacketReader reader)
        {
            Skill1 = reader.ReadByte();
            Skill2 = reader.ReadByte();
        }
    }
}
