using BattleShip.Common.Network;
using System.Collections.Generic;

namespace BattleShip.Common.Packets.Lobby
{
    public class C_RoomCreateReq : IPacket
    {
        public PacketId PacketId => PacketId.C_RoomCreateReq;

        public string RoomName { get; set; }
        public string ConfigName { get; set; } = "classic";

        public void Serialize(PacketWriter writer)
        {
            writer.Write(RoomName);
            writer.Write(ConfigName);
        }

        public void Deserialize(PacketReader reader)
        {
            RoomName = reader.ReadString();
            ConfigName = reader.ReadString();
        }
    }
}
