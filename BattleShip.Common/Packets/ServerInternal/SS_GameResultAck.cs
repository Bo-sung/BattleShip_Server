using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.ServerInternal
{
    public class SS_GameResultAck : IPacket
    {
        public PacketId PacketId => PacketId.SS_GameResultAck;

        public bool Success { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Success);
        }

        public void Deserialize(PacketReader reader)
        {
            Success = reader.ReadBool();
        }
    }
}
