using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Game
{
    public class S_TurnNotify : IPacket
    {
        public PacketId PacketId => PacketId.S_TurnNotify;

        public bool IsMyTurn { get; set; }
        public byte Mana { get; set; } = 0;  // StarBattle: 현재 마나, Classic/Extended: 0

        public void Serialize(PacketWriter writer)
        {
            writer.Write(IsMyTurn);
            writer.Write(Mana);
        }

        public void Deserialize(PacketReader reader)
        {
            IsMyTurn = reader.ReadBool();
            Mana = reader.ReadByte();
        }
    }
}
