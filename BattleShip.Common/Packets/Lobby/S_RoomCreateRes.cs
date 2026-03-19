using BattleShip.Common.Network;
using System.Collections.Generic;

namespace BattleShip.Common.Packets.Lobby
{
    public class S_RoomCreateRes : IPacket
    {
        public PacketId PacketId => PacketId.S_RoomCreateRes;

        public bool Success { get; set; }
        public string RoomId { get; set; }
        public string Message { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Success);
            writer.Write(RoomId);
            writer.Write(Message);
        }

        public void Deserialize(PacketReader reader)
        {
            Success = reader.ReadBool();
            RoomId = reader.ReadString();
            Message = reader.ReadString();
        }
    }
}
