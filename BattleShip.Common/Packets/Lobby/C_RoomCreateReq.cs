using BattleShip.Common.Network;
using System.Collections.Generic;

namespace BattleShip.Common.Packets.Lobby
{
    public class C_RoomCreateReq : IPacket
    {
        public PacketId PacketId => PacketId.C_RoomCreateReq;

        public string RoomName { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(RoomName);
        }

        public void Deserialize(PacketReader reader)
        {
            RoomName = reader.ReadString();
        }
    }
}
