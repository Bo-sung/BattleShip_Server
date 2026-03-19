using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.ServerInternal
{// ServerInternal/SS_Ping.cs
    public class SS_Ping : IPacket
    {
        public PacketId PacketId => PacketId.SS_Ping;

        public string SessionId { get; set; }
        public long Timestamp { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(SessionId);
            writer.Write(Timestamp);
        }

        public void Deserialize(PacketReader reader)
        {
            SessionId = reader.ReadString();
            Timestamp = reader.ReadLong();
        }
    }
}
