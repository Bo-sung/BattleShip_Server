using BattleShip.Common.Network;
using System.Collections.Generic;

namespace BattleShip.Common.Packets.Lobby
{
    public class S_RoomListRes : IPacket
    {
        public PacketId PacketId => PacketId.S_RoomListRes;

        public List<RoomInfo> Rooms { get; set; } = new List<RoomInfo>();

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Rooms.Count);
            foreach (var room in Rooms)
                room.Serialize(writer);
        }

        public void Deserialize(PacketReader reader)
        {
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
                Rooms.Add(RoomInfo.Deserialize(reader));
        }
    }
}
