using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.ServerInternal
{// ServerInternal/SS_Ping.cs
 // ServerInternal/SS_Pong.cs
    public class SS_Pong : IPacket
    {
        public PacketId PacketId => PacketId.SS_Pong;

        public long Timestamp { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Timestamp);
        }

        public void Deserialize(PacketReader reader)
        {
            Timestamp = reader.ReadLong();
        }
    }
}
