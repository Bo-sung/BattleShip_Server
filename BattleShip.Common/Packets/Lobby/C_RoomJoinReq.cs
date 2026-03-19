using BattleShip.Common.Network;

namespace BattleShip.Common.Packets.Lobby
{
    public class C_RoomJoinReq : IPacket
    {
        public PacketId PacketId => PacketId.C_RoomJoinReq;

        public string RoomId { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(RoomId);
        }

        public void Deserialize(PacketReader reader)
        {
            RoomId = reader.ReadString();
        }
    }
}
