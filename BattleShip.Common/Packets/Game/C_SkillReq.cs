using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Game
{
    public class C_SkillReq : IPacket
    {
        public PacketId PacketId => PacketId.C_SkillReq;

        public byte SkillType { get; set; }
        public byte TargetX { get; set; }     // 범위공격(중심), 이동스킬(목적지), 기타(무시)
        public byte TargetY { get; set; }
        public byte ShipType { get; set; }    // 이동/수리/실드 대상함선, 범위공격(무시)

        public void Serialize(PacketWriter writer)
        {
            writer.Write(SkillType);
            writer.Write(TargetX);
            writer.Write(TargetY);
            writer.Write(ShipType);
        }

        public void Deserialize(PacketReader reader)
        {
            SkillType = reader.ReadByte();
            TargetX = reader.ReadByte();
            TargetY = reader.ReadByte();
            ShipType = reader.ReadByte();
        }
    }
}
