using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Game
{
    public class S_SkillMoveRes : IPacket
    {
        public PacketId PacketId => PacketId.S_SkillMoveRes;

        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public byte Mana { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Success);
            writer.Write(Message);
            writer.Write(Mana);
        }

        public void Deserialize(PacketReader reader)
        {
            Success = reader.ReadBool();
            Message = reader.ReadString();
            Mana = reader.ReadByte();
        }
    }
}
