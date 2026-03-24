using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.ServerInternal
{
    public class SS_SessionReady : IPacket
    {
        public PacketId PacketId => PacketId.SS_SessionReady;

        public string SessionId { get; set; } = "";

        public void Serialize(PacketWriter writer)
        {
            writer.Write(SessionId);
        }

        public void Deserialize(PacketReader reader)
        {
            SessionId = reader.ReadString();
        }
    }
}
