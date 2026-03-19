using BattleShip.Common.Network;
using System.Collections.Generic;

namespace BattleShip.Common.Packets.Game
{
    public class C_EnterSessionReq : IPacket
    {
        public PacketId PacketId => PacketId.C_EnterSessionReq;

        public string SessionId { get; set; }

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
