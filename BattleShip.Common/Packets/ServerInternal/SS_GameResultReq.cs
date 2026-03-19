using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.ServerInternal
{// ServerInternal/SS_Ping.cs
 // ServerInternal/SS_GameResultReq.cs
    public class SS_GameResultReq : IPacket
    {
        public PacketId PacketId => PacketId.SS_GameResultReq;

        public string SessionId { get; set; }
        public string WinnerId { get; set; }
        public string LoserId { get; set; }
        public int TotalTurns { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(SessionId);
            writer.Write(WinnerId);
            writer.Write(LoserId);
            writer.Write(TotalTurns);
        }

        public void Deserialize(PacketReader reader)
        {
            SessionId = reader.ReadString();
            WinnerId = reader.ReadString();
            LoserId = reader.ReadString();
            TotalTurns = reader.ReadInt();
        }
    }
}
