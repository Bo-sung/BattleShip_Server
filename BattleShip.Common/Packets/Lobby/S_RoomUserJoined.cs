using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Lobby
{
    public class S_RoomUserJoined : IPacket
    {
        public PacketId PacketId => PacketId.S_RoomUserJoined;

        public string Username { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Username);
        }

        public void Deserialize(PacketReader reader)
        {
            Username = reader.ReadString();
        }
    }
}
