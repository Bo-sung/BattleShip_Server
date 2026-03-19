using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Game
{
    public class S_TurnNotify : IPacket
    {
        public PacketId PacketId => PacketId.S_TurnNotify;

        public bool IsMyTurn { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(IsMyTurn);
        }

        public void Deserialize(PacketReader reader)
        {
            IsMyTurn = reader.ReadBool();
        }
    }
}
