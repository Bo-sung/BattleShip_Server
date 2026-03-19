using BattleShip.Common.Network;
using System.Collections.Generic;

namespace BattleShip.Common.Packets.Lobby
{
    public class S_EnterLobbyRes : IPacket
    {
        public PacketId PacketId => PacketId.S_EnterLobbyRes;

        public bool Success { get; set; }
        public string Message { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Success);
            writer.Write(Message);
        }

        public void Deserialize(PacketReader reader)
        {
            Success = reader.ReadBool();
            Message = reader.ReadString();
        }
    }
}
